namespace Sango.Game
{
    public class Fire : SangoObject
    {
        public MapCoords coords;
        public int x { get { return coords.x; } }
        public int y { get { return coords.y; } }

        public Cell cell;
    }
}
