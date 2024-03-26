using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    public class BookType(string Name, string id)
    {
        public string Name { get; set; } = Name;
        public string id { get; set; } = id;
    }
}
