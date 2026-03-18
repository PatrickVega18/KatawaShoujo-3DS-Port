public static class HashHelper
{
    public static int GetID(string str)
    {
        if (string.IsNullOrEmpty(str)) return 0;
        unchecked
        {
            int hash = 23;
            foreach (char c in str)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}