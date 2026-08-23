using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;

var services = new ServiceCollection();
services.AddDataProtection().UseEphemeralDataProtectionProvider();
