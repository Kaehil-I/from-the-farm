namespace FromTheFarm.Api.Services;

// The only contract the generic repository needs: every stored document has a
// string id. All five models already declared exactly this property; this just
// makes it visible to MongoRepository<T> so it can build _id filters without
// reflection.
public interface IDocument
{
    string Id { get; set; }
}
