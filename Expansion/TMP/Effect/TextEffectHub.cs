public static class TextEffectHub
{
    private static readonly WaveWordTextEffect WaveWord = new();
    private static readonly WaveVerticeTextEffect WaveVertice = new();
    private static readonly ShakeTextEffect Shake = new();

    public static ITextEffect GetEffect(string linkID)
    {
        return linkID switch
        {
            "Wave_1" => WaveWord,
            "Wave_2" => WaveVertice,
            "Shake" => Shake,
            _ => null,
        };
    }
}
