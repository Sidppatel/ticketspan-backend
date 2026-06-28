using Amazon.S3;
using Amazon.S3.Model;

namespace Svyne.Api.Storage;

public sealed class ObjectStorage
{
    private readonly string? bucket;
    private readonly string? serviceUrl;
    private readonly string localRoot;

    public ObjectStorage(IConfiguration configuration)
    {
        bucket = configuration["S3_BUCKET"];
        serviceUrl = configuration["S3_SERVICE_URL"];
        localRoot = configuration["LOCAL_UPLOAD_ROOT"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
    }

    public bool UsesS3 => !string.IsNullOrEmpty(bucket);

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct)
    {
        if (UsesS3)
        {
            var config = new AmazonS3Config();
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                config.ForcePathStyle = true;
            }
            using var client = new AmazonS3Client(config);
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType
            }, ct);
            return;
        }

        var path = Path.Combine(localRoot, key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public async Task<Stream?> OpenReadAsync(string key, CancellationToken ct)
    {
        if (UsesS3)
        {
            var config = new AmazonS3Config();
            if (!string.IsNullOrEmpty(serviceUrl))
            {
                config.ServiceURL = serviceUrl;
                config.ForcePathStyle = true;
            }
            var client = new AmazonS3Client(config);
            try
            {
                var response = await client.GetObjectAsync(bucket, key, ct);
                return response.ResponseStream;
            }
            catch (AmazonS3Exception)
            {
                client.Dispose();
                return null;
            }
        }

        var path = Path.Combine(localRoot, key.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(path) ? File.OpenRead(path) : null;
    }
}
