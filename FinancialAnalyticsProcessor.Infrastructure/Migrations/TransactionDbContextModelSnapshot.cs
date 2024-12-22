﻿// <auto-generated />
using System;
using FinancialAnalyticsProcessor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace FinancialAnalyticsProcessor.Infrastructure.Migrations
{
    [DbContext(typeof(TransactionDbContext))]
    partial class TransactionDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.11")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("FinancialAnalyticsProcessor.Infrastructure.DbEntities.Transaction", b =>
                {
                    b.Property<Guid>("TransactionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Category")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset>("Date")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Description")
                        .IsRequired()
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Merchant")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("TransactionId");

                    b.HasIndex("Amount")
                        .HasDatabaseName("IX_Transaction_Amount");

                    b.HasIndex("Category")
                        .HasDatabaseName("IX_Transaction_Category");

                    b.HasIndex("Description")
                        .HasDatabaseName("IX_Transaction_Description");

                    b.HasIndex("UserId", "Date")
                        .HasDatabaseName("IX_Transaction_UserId_Date");

                    b.ToTable("Transactions", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}
