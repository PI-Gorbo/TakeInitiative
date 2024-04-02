using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TakeInitiative.BestiaryHandler.src.Json
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Book
    {
        public string print_contents()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }
        public string name { get; set; }
        public string id { get; set; }
        public string source { get; set; }
        public string group { get; set; }
        public Cover cover { get; set; }
        public string published { get; set; }
        public string author { get; set; }
        public List<Content> contents { get; set; }
        public List<string> alias { get; set; }
        public string coverUrl { get; set; }
    }

    public class Content
    {
        public string name { get; set; }
        public List<object> headers { get; set; }
        public Ordinal ordinal { get; set; }
    }

    public class Cover
    {
        public string type { get; set; }
        public string path { get; set; }
    }

    public class Ordinal
    {
        public string type { get; set; }
        public object identifier { get; set; }
    }

    public class Book_Root
    {
        public List<Book> book { get; set; }
    }

}
