using Marten;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TakeInitiative.BestiaryHandler.src.Json;

namespace TakeInitiative.BestiaryHandler.src.MartenDB
{
    public class PopulateDB(IDocumentSession session)
    {
        IDocumentSession session = session; //isnt this dumb?

        static string folder_path = ""; //this should be the root dir for the repo
        static string book_dir = ""; //Not sure how the repo will be organised yet
        static string beast_dir = "";//Not sure how the repo will be organised yet

        public async void Store_Books(IDocumentSession session)
        {
            //deserialise from file, throw exception if not found
            Book_Root root = JsonConvert.DeserializeObject<Book_Root>(File.ReadAllText(book_dir)) ?? throw new Exception("books.json not found!");

            //TODO: adding a task to the list makes it execute...... right?
            var tasks = new List<Task>();
            foreach (Book book in root.books)
            {
                tasks.Add(Store_Book(session, book));
            }
            await Task.WhenAll(tasks);


        }

        public async Task Store_Book(IDocumentSession session, Book book) //async store each book in the db
        {
            session.Store(new BookType(book.name, book.id));
            await session.SaveChangesAsync();
        }

    }
}
