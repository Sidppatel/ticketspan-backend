using System;
using Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Db.Migrations
{
    [DbContext(typeof(EventPlatformDbContext))]
    [Migration("20260814180000_AddPerformanceIndexes")]
    partial class AddPerformanceIndexes
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
        }
    }
}
