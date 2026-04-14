namespace Arena.AI.Trainer.Models;

public static class ModelRegistry
{
    public static IModelProfile Create(string modelName) => modelName switch
    {
        "dwarf" => new Dwarf.ModelProfile(),
        "scout" => new Scout.ModelProfile(),
        _ => throw new ArgumentException($"Unknown model: {modelName}. Available: dwarf, scout")
    };
}
