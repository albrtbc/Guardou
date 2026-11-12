using Guardou.Domain.Files;
using Microsoft.EntityFrameworkCore;

namespace Guardou.Infrastructure.Persistence;

internal static class ItemTreeQueries
{
    public static IQueryable<Item> Ancestry(DbSet<Item> items, string ownerId, Guid id)
    {
        return items.FromSql($"""
            WITH RECURSIVE "Chain"("Id") AS (
                SELECT "Id" FROM "Items" WHERE "Id" = {id} AND "OwnerId" = {ownerId}
                UNION
                SELECT "Parent"."Id" FROM "Chain"
                JOIN "Items" AS "Child" ON "Child"."Id" = "Chain"."Id"
                JOIN "Items" AS "Parent" ON "Parent"."Id" = "Child"."ParentId"
                WHERE "Parent"."OwnerId" = {ownerId}
            )
            SELECT * FROM "Items" WHERE "Id" IN (SELECT "Id" FROM "Chain")
            """);
    }

    public static IQueryable<Item> Subtree(DbSet<Item> items, Guid rootId, bool includeTrashed)
    {
        return items.FromSql($"""
            WITH RECURSIVE "Tree"("Id") AS (
                SELECT "Id" FROM "Items" WHERE "Id" = {rootId}
                UNION
                SELECT "Child"."Id" FROM "Tree"
                JOIN "Items" AS "Child" ON "Child"."ParentId" = "Tree"."Id"
                WHERE {includeTrashed} OR "Child"."Trashed" IS NULL
            )
            SELECT * FROM "Items" WHERE "Id" IN (SELECT "Id" FROM "Tree")
            """);
    }

    public static IQueryable<Item> Subtree(DbSet<Item> items, Guid rootId, bool includeTrashed, int limit)
    {
        // The LIMIT sits inside the CTE: on the outer select it would not stop the recursion, which would still walk the whole subtree.
        return items.FromSql($"""
            WITH RECURSIVE "Tree"("Id") AS (
                SELECT "Id" FROM "Items" WHERE "Id" = {rootId}
                UNION
                SELECT "Child"."Id" FROM "Tree"
                JOIN "Items" AS "Child" ON "Child"."ParentId" = "Tree"."Id"
                WHERE {includeTrashed} OR "Child"."Trashed" IS NULL
                LIMIT {limit}
            )
            SELECT * FROM "Items" WHERE "Id" IN (SELECT "Id" FROM "Tree")
            """);
    }

    public static List<Item> BreadthFirst(Guid rootId, IReadOnlyList<Item> items)
    {
        ILookup<Guid?, Item> children = items.ToLookup(item => item.ParentId);
        List<Item> ordered = items.Where(item => item.Id == rootId).ToList();
        HashSet<Guid> visited = [rootId];
        for (int position = 0; position < ordered.Count; position++)
        {
            ordered.AddRange(children[ordered[position].Id].Where(child => visited.Add(child.Id)));
        }

        return ordered;
    }
}
