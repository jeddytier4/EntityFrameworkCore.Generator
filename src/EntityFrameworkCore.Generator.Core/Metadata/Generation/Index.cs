using System.Collections.Generic;

namespace EntityFrameworkCore.Generator.Metadata.Generation
{
    public class Index : ModelBase
    {
        public string Name {get;set;}
        public List<string> Columns {get;set;}
        public bool IsUnique {get;set;}
        public string Filter {get;set;}
       
    }
}
