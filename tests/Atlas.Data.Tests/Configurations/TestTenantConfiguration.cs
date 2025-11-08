using Atlas.Data.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tests.Configurations
{
    public class TestTenantConfiguration : IEntityTypeConfiguration<TestTenant>
    {
        public void Configure(EntityTypeBuilder<TestTenant> builder)
        {
           builder.Property(t=>t.Id).ValueGeneratedNever();
        }
    }
}
