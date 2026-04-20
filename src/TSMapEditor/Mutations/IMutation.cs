namespace TSMapEditor.Mutations
{
    public interface IMutation
    {
        int EventID { get; }

        string GetDisplayString();
        void Perform();
        void Undo();
    }
}