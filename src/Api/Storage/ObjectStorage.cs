using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace TicketSpan.Api.Storage;

public sealed class ObjectStorage
{
    private readonly string? bucket;
    private readonly string localRoot;
    private readonly AmazonS3Client? s3Client;

    public ObjectStorage(IConfiguration configuration)
    {
        bucket = configuration["S3_BUCKET"];
        var serviceUrl = configuration["S3_SERVICE_URL"];
        var accessKey = configuration["AWS_ACCESS_KEY_ID"] ?? configuration["S3_ACCESS_KEY"] ?? configuration["MINIO_ROOT_USER"];
        var secretKey = configuration["AWS_SECRET_ACCESS_KEY"] ?? configuration["S3_SECRET_KEY"] ?? configuration["MINIO_ROOT_PASSWORD"];

        var rawLocalRoot = configuration["LOCAL_UPLOAD_ROOT"];
        localRoot = !string.IsNullOrWhiteSpace(rawLocalRoot)
            ? rawLocalRoot
            : Path.Combine(AppContext.BaseDirectory, "uploads");

        if (!string.IsNullOrEmpty(bucket))
        {
            var config = new AmazonS3Config
            {
                AuthenticationRegion = "us-east-1",
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                config.ForcePathStyle = true;
            }

            if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
            {
                s3Client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
            }
            else
            {
                s3Client = new AmazonS3Client(config);
            }
        }
    }

    public bool UsesS3 => !string.IsNullOrEmpty(bucket) && s3Client is not null;

    private static void TryResetPosition(Stream stream)
    {
        try
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
        }
        catch
        {
        }
    }

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        if (UsesS3 && s3Client is not null)
        {
            try
            {
                try
                {
                    var exists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, bucket!);
                    if (!exists)
                    {
                        await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = bucket! }, ct);
                    }
                }
                catch
                {
                }

                TryResetPosition(content);

                await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    InputStream = content,
                    ContentType = contentType,
                    DisablePayloadSigning = true,
                    UseChunkEncoding = false,
                    AutoCloseStream = false
                }, ct);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ObjectStorage] S3 PutAsync warning: {ex.Message}");
                TryResetPosition(content);
            }
        }

        TryResetPosition(content);
        var path = Path.Combine(localRoot, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        if (UsesS3 && s3Client is not null)
        {
            try
            {
                var response = await s3Client.GetObjectAsync(bucket, key, ct);
                return response.ResponseStream;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ObjectStorage] S3 OpenReadAsync warning: {ex.Message}");
            }
        }

        var path = Path.Combine(localRoot, key.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public async Task<string?> CreateImageRecordAsync(
        Data.Db db,
        Guid? userId,
        Guid? tenantId,
        string entityType,
        string entityId,
        string storageKey,
        string fileName,
        long length,
        string contentType,
        CancellationToken ct)
    {
        await using var connection = await db.OpenAsync(userId, tenantId, ct);
        await using var cmd = new Npgsql.NpgsqlCommand(
            "SELECT sp_create_image(@et, @eid, @key, @name, @size, 0, 0, 0, @uid, NULL, NULL, NULL, @ct, NULL, @t)", connection);
        cmd.Parameters.AddWithValue("et", string.IsNullOrEmpty(entityType) ? "generic" : entityType);
        cmd.Parameters.AddWithValue("eid", Guid.TryParse(entityId, out var eid) ? eid : Guid.Empty);
        cmd.Parameters.AddWithValue("key", storageKey);
        cmd.Parameters.AddWithValue("name", fileName);
        cmd.Parameters.AddWithValue("size", (int)length);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("ct", contentType);
        cmd.Parameters.AddWithValue("t", (object?)tenantId ?? DBNull.Value);
        var imageId = await cmd.ExecuteScalarAsync(ct);
        return imageId?.ToString();
    }

    public async Task<(string storageKey, string contentType)?> GetImageRecordAsync(Data.Db db, Guid imagesId, CancellationToken ct)
    {
        await using var connection = await db.OpenBootstrapAsync(ct);
        await using var cmd = new Npgsql.NpgsqlCommand("SELECT storage_key, content_type FROM vw_images WHERE images_id = @id", connection);
        cmd.Parameters.AddWithValue("id", imagesId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }
        var storageKey = reader.GetString(0);
        var contentType = reader.IsDBNull(1) ? "application/octet-stream" : reader.GetString(1);
        return (storageKey, contentType);
    }
}
