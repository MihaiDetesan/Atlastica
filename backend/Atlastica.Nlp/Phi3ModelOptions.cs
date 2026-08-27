namespace Atlastica.Nlp;

public class Phi3ModelOptions
{
    /// <summary>Directory containing the ONNX model + tokenizer files (see backend/models/download.sh).</summary>
    public string ModelPath { get; set; } = "../models/phi-3.5-mini-instruct-cpu-int4";
    public int MaxNewTokens { get; set; } = 400;
}
