namespace TSMapEditor.Mutations
{
    public interface ICheckableMutation : IMutation
    {
        bool ShouldPerform();
    }
}
