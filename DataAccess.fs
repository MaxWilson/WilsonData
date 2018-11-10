module DataAccess

open Microsoft.Azure.Services.AppAuthentication
open Microsoft.Azure.Documents.Client
open Microsoft.Azure.KeyVault
open System
open Microsoft.Azure.Documents
open System.Linq

type DynamicStorageRow =
  {
  id: string
  owner: string
  key: string
  ``type``: string
  value: obj
  isPublic: bool // can it be seen by people other than the owner?
  }

type RowSummary = { name: string; isPublic: bool }

let tok = AzureServiceTokenProvider()
let kv = new KeyVaultClient(fun authority resource scope -> tok.KeyVaultTokenCallback.Invoke(authority, resource, scope))
let kvUrl = "https://wilsondata.vault.azure.net/"
let dbUrl = kv.GetSecretAsync(kvUrl, "DB-url")
let primaryKey = kv.GetSecretAsync(kvUrl, "DB-secret")
let client = lazy (new DocumentClient(new Uri(dbUrl.Result.Value), primaryKey.Result.Value))

let dbMetadata = lazy(client.Value.CreateDatabaseIfNotExistsAsync(Database(Id = "WilsonData"), new RequestOptions()).Result.Resource)
let dbUri =
  lazy(UriFactory.CreateDatabaseUri(dbMetadata.Value.Id))
let collectionMetadata = lazy(
  let def =
    DocumentCollection(
      Id = "WilsonData",
      UniqueKeyPolicy = UniqueKeyPolicy (
        UniqueKeys =
          let coll = System.Collections.ObjectModel.Collection
          in
          coll [| UniqueKey(Paths = (coll [| "/owner"; "/key"; "/type" |])) |])
      )
  client.Value.CreateDocumentCollectionIfNotExistsAsync(dbUri.Value, def, RequestOptions()).Result.Resource)
let collectionUri =
  lazy(UriFactory.CreateDocumentCollectionUri(dbMetadata.Value.Id, collectionMetadata.Value.Id))

let list owner typename =
  client.Value.CreateDocumentQuery<_>(collectionUri.Value).Where(fun (d:DynamicStorageRow) -> d.owner = owner && d.``type`` = typename)
  |> Seq.map (fun v -> { name = v.key; isPublic = v.isPublic })
  |> List.ofSeq

let loadAll owner typename =
  client.Value.CreateDocumentQuery<_>(collectionUri.Value).Where(fun (d:DynamicStorageRow) -> d.owner = owner && d.``type`` = typename)
  |> Seq.map (fun v -> v.value)
  |> List.ofSeq

let load owner typename key =
  match client.Value.CreateDocumentQuery<_>(collectionUri.Value).Where(fun (d:DynamicStorageRow) -> d.owner = owner && d.``type`` = typename && d.key = key).Take(1) |> List.ofSeq with
  | v::_ ->
    Some v.value
  | [] ->
    None

let save isPublic owner typename key value =
  let record = {
    id = sprintf "%s.%s.%s" owner typename key
    owner = owner
    ``type`` = typename
    key = key
    value = value
    isPublic = isPublic
    }
  client.Value.UpsertDocumentAsync(collectionUri.Value, record).Wait()

let delete owner typename key =
  match client.Value.CreateDocumentQuery<_>(collectionUri.Value).Where(fun (d:DynamicStorageRow) -> d.owner = owner && d.``type`` = typename && d.key = key).Take(1) |> List.ofSeq with
  | record::_ ->
    client.Value.DeleteDocumentAsync(UriFactory.CreateDocumentUri(dbMetadata.Value.Id, collectionMetadata.Value.Id, record.id)).Wait()
    Some record.value
  | [] ->
    None
  
