using System.Collections.Generic;
using StashNet.Core.Network;

namespace StashNet.Network
{
    /// <summary>
    /// Finds Cores and Drive Bays by walking the tile entities of the chunks around a point, instead of
    /// checking every block (Milestone 8). At reach 24 a block-by-block cube is 117,649 blocks; the
    /// chunks it overlaps hold only a handful of tile entities. Unloaded chunks are skipped.
    /// </summary>
    internal static class TileEntityScan
    {
        /// <summary>Composite tile entities (Cores, bays, ...) within cube distance <paramref name="r"/> of <paramref name="center"/>.</summary>
        public static IEnumerable<TileEntityComposite> InCube(World world, Vector3i center, int r)
        {
            int minCx = World.toChunkXZ(center.x - r), maxCx = World.toChunkXZ(center.x + r);
            int minCz = World.toChunkXZ(center.z - r), maxCz = World.toChunkXZ(center.z + r);
            for (int cx = minCx; cx <= maxCx; cx++)
            {
                for (int cz = minCz; cz <= maxCz; cz++)
                {
                    if (!(world.GetChunkSync(cx, cz) is Chunk chunk)) continue;
                    List<TileEntity> list = chunk.GetTileEntities()?.list;
                    if (list == null) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!(list[i] is TileEntityComposite te)) continue;
                        Vector3i pos = te.ToWorldPos();
                        if (CorePicker.CubeDistance(pos.x, pos.y, pos.z, center.x, center.y, center.z) > r) continue;
                        yield return te;
                    }
                }
            }
        }
    }
}
