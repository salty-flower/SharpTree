namespace SharpTree;

public interface ITreeWalker
{
    (int dirs, int files) Walk(
        string rootPath,
        bool includeFiles,
        int maxDepth,
        TreeRenderer renderer
    );
}
