using System.ComponentModel;

namespace EntityFrameworkCore.Generator.Options
{
    /// <summary>
    /// EntityFramework mapping class generation options
    /// </summary>
    /// <seealso cref="ClassOptionsBase" />
    public class MappingClassOptions : ClassOptionsBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MappingClassOptions"/> class.
        /// </summary>
        public MappingClassOptions(VariableDictionary variables, string prefix)
            : base(variables, AppendPrefix(prefix, "Mapping"))
        {
            Namespace = "{Project.Namespace}.Data.Mapping";
            Directory = @"{Project.Directory}\Data\Mapping";
            IncludeConstants = true;
        }

        /// <summary>
        /// If true generate constants for table name and columns 
        /// </summary>
        [DefaultValue(true)]
        public bool IncludeConstants { get; set; }
        [DefaultValue("Map")]
        public string Suffix { get; set; }
    }
}