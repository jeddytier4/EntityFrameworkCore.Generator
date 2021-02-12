using System.Globalization;
using System.Linq;
using EntityFrameworkCore.Generator.Extensions;
using EntityFrameworkCore.Generator.Metadata.Generation;
using EntityFrameworkCore.Generator.Options;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.Generator.Templates
{
    public class MappingClassTemplate : CodeTemplateBase
    {
        private readonly Entity _entity;

        public MappingClassTemplate(Entity entity, GeneratorOptions options) : base(options)
        {
            _entity = entity;
        }


        public override string WriteCode()
        {
            if (Options.Data.Entity.SingleFileWithMapping)
            {
                using (CodeBuilder.Indent())
                {
                    CodeBuilder.DecrementIndent();
                    GenerateClass();
                }
            }
            else
            {
                CodeBuilder.Clear();

                CodeBuilder.AppendLine("using Microsoft.EntityFrameworkCore;");
                CodeBuilder.AppendLine("using System;");
                CodeBuilder.AppendLine("using System.Collections.Generic;");
                CodeBuilder.AppendLine();

                CodeBuilder.AppendLine($"namespace {_entity.MappingNamespace}");
                CodeBuilder.AppendLine("{");

                using (CodeBuilder.Indent())
                {
                    GenerateClass();
                }

                CodeBuilder.AppendLine("}");
            }

            return CodeBuilder.ToString();
        }


        private void GenerateClass()
        {
            var mappingClass = _entity.MappingClass.ToSafeName();
            var entityClass = _entity.EntityClass.ToSafeName();
            var safeName = $"{_entity.EntityNamespace}.{entityClass}";

            if (Options.Data.Mapping.Document)
            {
                CodeBuilder.AppendLine("/// <summary>");
                CodeBuilder.AppendLine($"/// Allows configuration for an entity type <see cref=\"{safeName}\" />");
                CodeBuilder.AppendLine("/// </summary>");
            }

            CodeBuilder.Append($"internal class {mappingClass} ");

            using (CodeBuilder.Indent())
                CodeBuilder.AppendLine($": IEntityTypeConfiguration<{entityClass}>");

            CodeBuilder.AppendLine("{");

            using (CodeBuilder.Indent())
            {
                GenerateConfigure();
                if (Options.Data.Mapping.IncludeConstants)
                { GenerateConstants(); }
            }

            CodeBuilder.AppendLine("}");

        }

        private void GenerateConstants()
        {
            var entityClass = _entity.EntityClass.ToSafeName();
            var safeName = $"{_entity.EntityNamespace}.{entityClass}";

            //CodeBuilder.AppendLine("#region Generated Constants");

            CodeBuilder.AppendLine("public struct Table");
            CodeBuilder.AppendLine("{");

            using (CodeBuilder.Indent())
            {

                if (Options.Data.Mapping.Document)
                    CodeBuilder.AppendLine($"/// <summary>Table Schema name constant for entity <see cref=\"{safeName}\" /></summary>");

                CodeBuilder.AppendLine($"public const string Schema = \"{_entity.TableSchema}\";");

                if (Options.Data.Mapping.Document)
                    CodeBuilder.AppendLine($"/// <summary>Table Name constant for entity <see cref=\"{safeName}\" /></summary>");

                CodeBuilder.AppendLine($"public const string Name = \"{_entity.TableName}\";");
            }

            CodeBuilder.AppendLine("}");

            CodeBuilder.AppendLine();
            CodeBuilder.AppendLine("public struct Columns");
            CodeBuilder.AppendLine("{");

            using (CodeBuilder.Indent())
            {
                foreach (var property in _entity.Properties)
                {
                    if (Options.Data.Mapping.Document)
                        CodeBuilder.AppendLine($"/// <summary>Column Name constant for property <see cref=\"{safeName}.{property.PropertyName}\" /></summary>");

                    CodeBuilder.AppendLine($"public const string {property.PropertyName.ToSafeName()} = {property.ColumnName.ToLiteral()};");
                }
            }

            CodeBuilder.AppendLine("}");
            //CodeBuilder.AppendLine("#endregion");
        }

        private void GenerateConfigure()
        {
            var entityClass = _entity.EntityClass.ToSafeName();
            var entityFullName = $"{_entity.EntityNamespace}.{entityClass}";

            if (Options.Data.Mapping.Document)
            {
                CodeBuilder.AppendLine("/// <summary>");
                CodeBuilder.AppendLine($"/// Configures the entity of type <see cref=\"{entityFullName}\" />");
                CodeBuilder.AppendLine("/// </summary>");
                CodeBuilder.AppendLine("/// <param name=\"builder\">The builder to be used to configure the entity type.</param>");
            }

            CodeBuilder.AppendLine($"public void Configure(EntityTypeBuilder<{entityClass}> builder)");
            CodeBuilder.AppendLine("{");

            using (CodeBuilder.Indent())
            {
                //CodeBuilder.AppendLine("#region Generated Configure");

                GenerateTableMapping();
                GenerateKeyMapping();
                GeneratePropertyMapping();
                GenerateRelationshipMapping();
                GenerateIndexMapping();
                //CodeBuilder.AppendLine("#endregion");
            }

            CodeBuilder.AppendLine("}");
            CodeBuilder.AppendLine();
        }


        private void GenerateRelationshipMapping()
        {
            if (_entity.Relationships.Count > 0 && (_entity.Relationships.Where(e => e.IsMapped).ToList().Count > 0))
            {
                CodeBuilder.AppendLine("// relationships");
            }

            foreach (var relationship in _entity.Relationships.Where(e => e.IsMapped))
            {
                GenerateRelationshipMapping(relationship);
                CodeBuilder.AppendLine();
            }

        }

        private void GenerateRelationshipMapping(Relationship relationship)
        {
            var hasOnePropertyName = relationship.PropertyName
                .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                .Replace(Options.Data.Entity.Suffix, "")
                .ToSafeName();
            var propName = hasOnePropertyName.Contains("_") ? hasOnePropertyName : hasOnePropertyName.Pascalize();
            CodeBuilder.Append("builder.HasOne(e => e.");
            CodeBuilder.Append(propName);
            CodeBuilder.Append(")");
            CodeBuilder.AppendLine();

            CodeBuilder.IncrementIndent();

            CodeBuilder.Append(relationship.PrimaryCardinality == Cardinality.Many
                ? ".WithMany(p => p."
                : ".WithOne(p => p.");
            var primaryPropertyName = relationship.PrimaryCardinality == Cardinality.Many
                ? relationship.PrimaryPropertyName
                    .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                    .Replace(Options.Data.Entity.Suffix, "")
                    .Pluralize(false).ToSafeName()
                : relationship.PrimaryPropertyName
                    .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                    .Replace(Options.Data.Entity.Suffix, "")
                    .ToSafeName();
            propName = primaryPropertyName.Contains("_") ? primaryPropertyName : primaryPropertyName.Pascalize();
            CodeBuilder.Append(propName);
            CodeBuilder.Append(")");

            CodeBuilder.AppendLine();
            CodeBuilder.Append(".HasForeignKey");
            if (relationship.IsOneToOne)
            {
                CodeBuilder.Append("<");
                //CodeBuilder.Append(_entity.EntityNamespace);
                //CodeBuilder.Append(".");
                CodeBuilder.Append(_entity.EntityClass.ToSafeName());
                CodeBuilder.Append(">");
            }
            CodeBuilder.Append("(e => ");

            var keys = relationship.Properties;
            var wroteLine = false;

            if (keys.Count == 1)
            {
                var propertyName = Options.Data.Entity.RelationshipNaming == RelationshipNaming.Suffix
                    ? keys.First().PropertyName
                        .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                        .Replace(Options.Data.Entity.Suffix, "")
                        .Pluralize(false).ToSafeName()
                    : keys.First().PropertyName
                        .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                        .Replace(Options.Data.Entity.Suffix, "")
                        .ToSafeName();
                propName = propertyName.Contains("_") ? propertyName : propertyName.Pascalize();
                //var propertyName = keys.First().PropertyName.ToSafeName();
                CodeBuilder.Append($"e.{propName}");
            }
            else
            {
                CodeBuilder.Append("new { ");
                foreach (var p in keys)
                {
                    var propertyName = Options.Data.Entity.RelationshipNaming == RelationshipNaming.Suffix
                        ? p.PropertyName
                            .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                            .Replace(Options.Data.Entity.Suffix, "")
                            .Pluralize(false).ToSafeName()
                        : p.PropertyName
                            .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                            .Replace(Options.Data.Entity.Suffix, "")
                            .ToSafeName();
                    propName = propertyName.Contains("_") ? propertyName : propertyName.Pascalize();
                    if (wroteLine)
                        CodeBuilder.Append(", ");

                    CodeBuilder.Append($"e.{propName}");
                    wroteLine = true;
                }
                CodeBuilder.Append("}");
            }
            CodeBuilder.Append(")");
            CodeBuilder.AppendLine();
            CodeBuilder.Append(".OnDelete(DeleteBehavior.Restrict)");

            //_{ string.Join("_", keys.Select(r => r.ColumnName))}
            var testForeignKeyName = $"FK_{_entity.TableSchema}_{_entity.TableName}_{relationship.PrimaryEntity.TableName.Singularize(false)}";
            if (!string.IsNullOrEmpty(relationship.RelationshipName) && !(testForeignKeyName == relationship.RelationshipName))
            {
                CodeBuilder.AppendLine();
                CodeBuilder.Append(".HasConstraintName(\"");
                CodeBuilder.Append(relationship.RelationshipName);
                CodeBuilder.Append("\")");
            }

            CodeBuilder.DecrementIndent();

            CodeBuilder.AppendLine(";");
        }


        private void GeneratePropertyMapping()
        {
            if (_entity.Properties.Count > 0)
            {
                CodeBuilder.AppendLine("// properties");
            }
            foreach (var property in _entity.Properties)
            {
                GeneratePropertyMapping(property);
            }
        }

        private void GeneratePropertyMapping(Property property)
        {
            var isString = property.SystemType == typeof(string);
            var isByteArray = property.SystemType == typeof(byte[]);
            var isMoreThanOneLine = false;
            var tempBuilder = new IndentedStringBuilder();
            var propName = property.PropertyName.Contains("_") ? property.PropertyName : property.PropertyName.Pascalize();
            tempBuilder.Append($"builder.Property(e => e.{propName})");

            tempBuilder.IncrementIndent();
            //if (property.IsRequired)
            //{
            //    CodeBuilder.AppendLine();
            //    CodeBuilder.Append(".IsRequired()");
            //}

            if (property.IsRowVersion == true)
            {
                tempBuilder.AppendLine();
                tempBuilder.Append(".IsRowVersion()");
                tempBuilder.AppendLine();
                tempBuilder.Append(".IsRequired()");
                tempBuilder.AppendLine();
                tempBuilder.Append(".HasConversion(new NumberToBytesConverter<ulong>())");
                tempBuilder.AppendLine();
                tempBuilder.Append(".HasColumnType(\"rowversion\")");
                isMoreThanOneLine = true;
            }
            if (propName.StartsWith("_"))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append($".HasColumnName({property.ColumnName.ToLiteral()})");
            }


            if (!string.IsNullOrEmpty(property.StoreType) && (property.StoreType.ToLiteral().StartsWith("\"decimal") || property.StoreType.ToLiteral().StartsWith("\"date") || property.IsHierarchyId.GetValueOrDefault()))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append($".HasColumnType({property.StoreType.ToLiteral()})");
                isMoreThanOneLine = true;
            }
            if (isString && property.StoreType.ToLiteral().StartsWith("\"varchar"))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append(".IsUnicode(false)");
                if (!property.IsNullable.GetValueOrDefault())
                {
                    tempBuilder.AppendLine();
                    tempBuilder.Append(".IsRequired(true)");
                }
                else
                {
                    tempBuilder.AppendLine();
                    tempBuilder.Append(".IsRequired(false)");
                }
                isMoreThanOneLine = true;
            }

            if ((isString || isByteArray) && property.Size > 0)
            {
                if (!property.IsRowVersion.GetValueOrDefault() && !property.IsHierarchyId.GetValueOrDefault())
                {
                    tempBuilder.AppendLine();
                    tempBuilder.Append($".HasMaxLength({property.Size.Value.ToString(CultureInfo.InvariantCulture)})");
                    isMoreThanOneLine = true;
                }

            }

            if (!string.IsNullOrEmpty(property.Default))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append($".HasDefaultValueSql({property.Default.ToLiteral().Replace("(", "").Replace(")", "")})");
                isMoreThanOneLine = true;
            }

            if (!property.IsRowVersion.GetValueOrDefault())
            {
                switch (property.ValueGenerated)
                {
                    case ValueGenerated.OnAdd:
                        if (!string.IsNullOrEmpty(property.ComputedSql))
                        {
                            tempBuilder.AppendLine();
                            tempBuilder.Append($".HasComputedColumnSql(\"{property.ComputedSql}\")");
                        }
                        tempBuilder.AppendLine();
                        tempBuilder.Append(".ValueGeneratedOnAdd()");
                        isMoreThanOneLine = true;
                        break;
                    case ValueGenerated.OnAddOrUpdate:
                        if (!string.IsNullOrEmpty(property.ComputedSql))
                        {
                            tempBuilder.AppendLine();
                            tempBuilder.Append($".HasComputedColumnSql(\"{property.ComputedSql}\")");
                        }
                        tempBuilder.AppendLine();
                        tempBuilder.Append(".ValueGeneratedOnAddOrUpdate()");
                        isMoreThanOneLine = true;
                        break;
                    case ValueGenerated.OnUpdate:
                        if (!string.IsNullOrEmpty(property.ComputedSql))
                        {
                            tempBuilder.AppendLine();
                            tempBuilder.Append($".HasComputedColumnSql(\"{property.ComputedSql}\")");
                        }
                        tempBuilder.AppendLine();
                        tempBuilder.Append(".ValueGeneratedOnUpdate()");
                        isMoreThanOneLine = true;
                        break;
                }
            }
            tempBuilder.DecrementIndent();

            tempBuilder.AppendLine(";");
            if (isMoreThanOneLine)
            {
                CodeBuilder.AppendLines(tempBuilder.ToString());
                CodeBuilder.AppendLine();
            }
        }


        private void GenerateKeyMapping()
        {


            var keys = _entity.Properties.Where(p => p.IsPrimaryKey == true).ToList();

            if (keys.Count == 0)
            {
                CodeBuilder.AppendLine("// key");

                CodeBuilder.AppendLine("builder.HasNoKey();");
                CodeBuilder.AppendLine();

                return;
            }

            CodeBuilder.AppendLine("// key");
            CodeBuilder.Append("builder.HasKey(e => ");

            if (keys.Count == 1)
            {
                var propertyName = keys.First().PropertyName.ToSafeName().Contains("_") ? keys.First().PropertyName.ToSafeName() : keys.First().PropertyName.ToSafeName().Pascalize();
                CodeBuilder.AppendLine($"e.{propertyName});");
                CodeBuilder.AppendLine();

                return;
            }

            var wroteLine = false;

            CodeBuilder.Append("new { ");
            foreach (var p in keys)
            {
                if (wroteLine)
                    CodeBuilder.Append(", ");

                CodeBuilder.Append("e.");
                var propName = p.PropertyName.Contains("_") ? p.PropertyName : p.PropertyName.Pascalize();
                CodeBuilder.Append(p.PropertyName);
                wroteLine = true;
            }

            CodeBuilder.AppendLine(" });");
            CodeBuilder.AppendLine();
        }

        private void GenerateTableMapping()
        {
            CodeBuilder.AppendLine("// custom generator settings");
            CodeBuilder.AppendLine("builder.GenerateHistoryEntity(false);");
            CodeBuilder.AppendLine("builder.GenerateDalCriteriaClass(false);");
            CodeBuilder.AppendLine("builder.GenerateDalFieldsClass(false);");
            CodeBuilder.AppendLine("builder.GenerateEnumCheckConstraints(false);");
            CodeBuilder.AppendLine();



            CodeBuilder.AppendLine("// table");

            var method = _entity.IsView
                ? nameof(RelationalEntityTypeBuilderExtensions.ToView)
                : nameof(RelationalEntityTypeBuilderExtensions.ToTable);

            CodeBuilder.AppendLine(_entity.TableSchema.HasValue()
                ? $"builder.{method}(\"{_entity.TableName}\", \"{_entity.TableSchema}\");"
                : $"builder.{method}(\"{_entity.TableName}\");");

            CodeBuilder.AppendLine();
            foreach (var uc in _entity.UniqueConstraints)
            {
                CodeBuilder.Append(uc.Columns.Count > 1
                    ? $"builder.HasAlternateKey(e => new {{e.{string.Join(", e.", uc.Columns.ToArray())}}})"
                    : $"builder.HasAlternateKey(e => e.{uc.Columns[0]})");
                CodeBuilder.IncrementIndent();
                CodeBuilder.AppendLine();
                CodeBuilder.Append($".HasName(\"{uc.Name}\")");
                CodeBuilder.DecrementIndent();
                CodeBuilder.AppendLine(";");
            }
            CodeBuilder.AppendLine();
        }
        private void GenerateIndexMapping()
        {
            if (_entity.Indexes.Count > 0)
            {
                CodeBuilder.AppendLine("// indexes");
            }
            foreach (var index in _entity.Indexes)
            {
                var indexName = index.Name.ToSafeName();

                CodeBuilder.Append(index.Columns.Count > 1
                    ? $"builder.HasIndex(e => new {{e.{string.Join(", e.", index.Columns.ToArray())}}})"
                    : $"builder.HasIndex(e => e.{index.Columns[0]})");
                CodeBuilder.IncrementIndent();
                var testIndexName = $"IX_{_entity.TableName}_{ string.Join("_", index.Columns.ToArray())}";
                if (!(testIndexName == indexName))
                {

                    CodeBuilder.AppendLine();
                    CodeBuilder.Append($".HasName(\"{indexName}\")");
                }

                if (!string.IsNullOrEmpty(index.Filter))
                {
                    CodeBuilder.AppendLine();
                    CodeBuilder.Append($".HasFilter(\"{index.Filter}\")");
                }
                if (index.IsUnique)
                {
                    CodeBuilder.AppendLine();
                    CodeBuilder.Append(".IsUnique()");
                }

                CodeBuilder.DecrementIndent();
                CodeBuilder.AppendLine(";");
                CodeBuilder.AppendLine();
            }

        }
    }
}
