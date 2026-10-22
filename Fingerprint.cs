// Empreinte des fichiers selon CurseForge : MurmurHash2 (graine 1) sur le contenu privé des octets 9, 10, 13 et 32.
// Modifie le tableau reçu (filtrage sur place, pour ne pas copier des .jar de plusieurs Mo).
static class Fingerprint
{
    public static uint Of(byte[] data)
    {
        int n = 0;
        foreach (var b in data) if (b is not (9 or 10 or 13 or 32)) data[n++] = b;
        const uint m = 0x5bd1e995;
        uint h = 1 ^ (uint)n;
        int i = 0;
        for (; i + 4 <= n; i += 4)
        {
            uint k = BitConverter.ToUInt32(data, i);
            k *= m; k ^= k >> 24; k *= m;
            h *= m; h ^= k;
        }
        switch (n - i)
        {
            case 3: h ^= (uint)data[i + 2] << 16; goto case 2;
            case 2: h ^= (uint)data[i + 1] << 8; goto case 1;
            case 1: h ^= data[i]; h *= m; break;
        }
        h ^= h >> 13; h *= m; h ^= h >> 15;
        return h;
    }
}
