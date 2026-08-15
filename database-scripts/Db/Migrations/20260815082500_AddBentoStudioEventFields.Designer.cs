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
    [Migration("20260815082500_AddBentoStudioEventFields")]
    partial class AddBentoStudioEventFields
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
        }
    }
}
