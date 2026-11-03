namespace Studio.Audio
{
    /// Identificador de un sonido que está sonando. Sirve para pararlo o cambiarle el volumen
    /// (por ejemplo, un bucle). Si el sonido ya terminó, el identificador deja de valer sin dar error.
    public readonly struct AudioHandle
    {
        public readonly int Id;

        public AudioHandle(int id) { Id = id; }

        public bool IsValid => Id > 0;

        public static AudioHandle None => default;
    }
}
