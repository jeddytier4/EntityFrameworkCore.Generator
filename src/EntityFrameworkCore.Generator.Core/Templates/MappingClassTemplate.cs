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
                    GenerateClass();
                }
            }
            else
            {
                CodeBuilder.Clear();

                CodeBuilder.AppendLine("using System;");
                CodeBuilder.AppendLine("using System.Collections.Generic;");
                CodeBuilder.AppendLine("using Microsoft.EntityFrameworkCore;");
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

            CodeBuilder.AppendLine($"internal class {mappingClass}");

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

            CodeBuilder.AppendLine($"public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<{entityClass}> builder)");
            CodeBuilder.AppendLine("{");

            using (CodeBuilder.Indent())
            {
                //CodeBuilder.AppendLine("#region Generated Configure");

                GenerateTableMapping();
                GenerateKeyMapping();
                GenerateIndexMapping();
                GeneratePropertyMapping();
                GenerateRelationshipMapping();

                //CodeBuilder.AppendLine("#endregion");
            }

            CodeBuilder.AppendLine("}");
            CodeBuilder.AppendLine();
        }


        private void GenerateRelationshipMapping()
        {
            if (_entity.Relationships.Count > 0)
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

            CodeBuilder.Append("builder.HasOne(t => t.");
            CodeBuilder.Append(relationship.PropertyName);
            CodeBuilder.Append(")");
            CodeBuilder.AppendLine();

            CodeBuilder.IncrementIndent();

            CodeBuilder.Append(relationship.PrimaryCardinality == Cardinality.Many
                ? ".WithMany(t => t."
                : ".WithOne(t => t.");
            var primaryPropertyName = Options.Data.Entity.RelationshipNaming == RelationshipNaming.Plural
                ? relationship.PrimaryPropertyName
                    .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                    .Replace(Options.Data.Entity.Suffix, "")
                    .Pluralize(false).Pascalize().ToSafeName()
                : relationship.PrimaryPropertyName
                    .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                    .Replace(Options.Data.Entity.Suffix, "")
                    .Pascalize().ToSafeName();
            CodeBuilder.Append(primaryPropertyName);
            CodeBuilder.Append(")");

            CodeBuilder.AppendLine();
            CodeBuilder.Append(".HasForeignKey");
            if (relationship.IsOneToOne)
            {
                CodeBuilder.Append("<");
                CodeBuilder.Append(_entity.EntityNamespace);
                CodeBuilder.Append(".");
                CodeBuilder.Append(_entity.EntityClass.ToSafeName());
                CodeBuilder.Append(">");
            }
            CodeBuilder.Append("(d => ");

            var keys = relationship.Properties;
            var wroteLine = false;

            if (keys.Count == 1)
            {
                var propertyName = Options.Data.Entity.RelationshipNaming == RelationshipNaming.Plural
                    ? keys.First().PropertyName
                        .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                        .Replace(Options.Data.Entity.Suffix, "")
                        .Pluralize(false).Pascalize().ToSafeName()
                    : keys.First().PropertyName
                        .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                        .Replace(Options.Data.Entity.Suffix, "")
                        .Pascalize().ToSafeName();
                //var propertyName = keys.First().PropertyName.ToSafeName();
                CodeBuilder.Append($"d.{propertyName}");
            }
            else
            {
                CodeBuilder.Append("new { ");
                foreach (var p in keys)
                {
                    var propertyName = Options.Data.Entity.RelationshipNaming == RelationshipNaming.Plural
                        ? p.PropertyName
                            .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                            .Replace(Options.Data.Entity.Suffix, "")
                            .Pluralize(false).Pascalize().ToSafeName()
                        :p.PropertyName
                            .Replace(Options.Data.Entity.Suffix.Pluralize(true), "")
                            .Replace(Options.Data.Entity.Suffix, "")
                            .Pascalize().ToSafeName();
                    if (wroteLine)
                        CodeBuilder.Append(", ");

                    CodeBuilder.Append($"d.{propertyName}");
                    wroteLine = true;
                }
                CodeBuilder.Append("}");
            }
            CodeBuilder.Append(")");

            if (!string.IsNullOrEmpty(relationship.RelationshipName))
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
            tempBuilder.Append($"builder.Property(t => t.{property.ColumnName})");

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
                isMoreThanOneLine = true;
            }

            //CodeBuilder.AppendLine();
            //CodeBuilder.Append($".HasColumnName({property.ColumnName.ToLiteral()})");

            if (!string.IsNullOrEmpty(property.StoreType) && (property.StoreType.ToLiteral().StartsWith("\"decimal") || property.StoreType.ToLiteral().StartsWith("\"date")))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append($".HasColumnType({property.StoreType.ToLiteral()})");
                isMoreThanOneLine = true;
            }
            if (isString && property.StoreType.ToLiteral().StartsWith("\"varchar"))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append(".IsUnicode(false)");
                isMoreThanOneLine = true;
            }

            if ((isString || isByteArray) && property.Size > 0)
            {
                tempBuilder.AppendLine();
                tempBuilder.Append($".HasMaxLength({property.Size.Value.ToString(CultureInfo.InvariantCulture)})");
                isMoreThanOneLine = true;
            }

            if (!string.IsNullOrEmpty(property.Default))
            {
                tempBuilder.AppendLine();
                tempBuilder.Append($".HasDefaultValueSql({property.Default.ToLiteral().Replace("(", "").Replace(")", "")})");
                isMoreThanOneLine = true;
            }

            switch (property.ValueGenerated)
            {
                case ValueGenerated.OnAdd:
                    tempBuilder.AppendLine();
                    tempBuilder.Append(".ValueGeneratedOnAdd()");
                    isMoreThanOneLine = true;
                    break;
                case ValueGenerated.OnAddOrUpdate:
                    tempBuilder.AppendLine();
                    tempBuilder.Append(".ValueGeneratedOnAddOrUpdate()");
                    isMoreThanOneLine = true;
                    break;
                case ValueGenerated.OnUpdate:
                    tempBuilder.AppendLine();
                    tempBuilder.Append(".ValueGeneratedOnUpdate()");
                    isMoreThanOneLine = true;
                    break;
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

            CodeBuilder.AppendLine(keys.Count == 1 ? "// key" : "// keys");
            CodeBuilder.Append("builder.HasKey(t => ");

            if (keys.Count == 1)
            {
                var propertyName = keys.First().PropertyName.ToSafeName();
                CodeBuilder.AppendLine($"t.{propertyName});");
                CodeBuilder.AppendLine();

                return;
            }

            var wroteLine = false;

            CodeBuilder.Append("new { ");
            foreach (var p in keys)
            {
                if (wroteLine)
                    CodeBuilder.Append(", ");

                CodeBuilder.Append("t.");
                CodeBuilder.Append(p.PropertyName);
                wroteLine = true;
            }

            CodeBuilder.AppendLine(" });");
            CodeBuilder.AppendLine();
        }

        private void GenerateTableMapping()
        {
            CodeBuilder.AppendLine("// table");

            var method = _entity.IsView
                ? nameof(RelationalEntityTypeBuilderExtensions.ToView)
                : nameof(RelationalEntityTypeBuilderExtensions.ToTable);

            CodeBuilder.AppendLine(_entity.TableSchema.HasValue()
                ? $"builder.{method}(\"{_entity.TableName}\", \"{_entity.TableSchema}\");"
                : $"builder.{method}(\"{_entity.TableName}\");");

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
                
                CodeBuilder.AppendLine(index.Columns.Count > 1
                    ? $"builder.HasIndex(e => new {{e.{string.Join(", e.", index.Columns.ToArray())}}})"
                    : $"builder.HasIndex(e => e.{index.Columns[0]})");

                CodeBuilder.IncrementIndent();
                CodeBuilder.Append($".HasName(\"{indexName}\")");
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
