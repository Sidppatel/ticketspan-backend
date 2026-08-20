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
    [Migration("20260820054000_FixBookingAndTicketAccessSecurity")]
    partial class FixBookingAndTicketAccessSecurity
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
        }
    }
}
