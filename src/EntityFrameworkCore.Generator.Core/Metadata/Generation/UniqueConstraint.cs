using System.Collections.Generic;

namespace EntityFrameworkCore.Generator.Metadata.Generation
{
    public class UniqueConstraint : ModelBase
    {
        public string Name {get;set;}
        public List<string> Columns {get;set;}
    }
}
