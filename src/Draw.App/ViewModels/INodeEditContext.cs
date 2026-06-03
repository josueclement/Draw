using System.Collections.Generic;

namespace Draw.App.ViewModels;

/// <summary>
/// The document-level services a class node and its members need: undo capture before an edit,
/// dirty marking after, and the current set of type-name autocomplete suggestions.
/// </summary>
public interface INodeEditContext
{
    void BeginMemberEdit();

    void EndMemberEdit();

    IReadOnlyList<string> GetTypeSuggestions();
}
