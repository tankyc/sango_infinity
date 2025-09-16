namespace Sango.Game
{
    public interface IAarryDataObject
    {
        public abstract IAarryDataObject FromArray(int[] values);
        public abstract int[] ToArray();
    }
}
