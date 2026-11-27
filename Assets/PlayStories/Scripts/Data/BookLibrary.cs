using System.Collections.Generic;
using UnityEngine;

namespace PlayStories
{
    /// <summary>All books shown in the library, in display order.</summary>
    [CreateAssetMenu(menuName = "PlayStories/Library", fileName = "Library")]
    public class BookLibrary : ScriptableObject
    {
        public List<BookDefinition> books = new List<BookDefinition>();
    }
}
