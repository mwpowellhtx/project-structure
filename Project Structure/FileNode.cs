using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using NLog;

namespace ProjectStructure {
    public interface IFileNode : IProjectNode {
        byte[] Data { get; set; }
        string Text { get; set; }
    }

    public class FileNode : IFileNode {
        public event EventHandler<PreviewNodeDeletedEventArgs> PreviewDeleted;
        public event EventHandler<PreviewNodeRenamedEventArgs> PreviewRenamed;
        public event EventHandler<PreviewNodeMovedEventArgs> PreviewMoved;
        public event EventHandler<PreviewNodeModifiedEventArgs> PreviewModified;

        public event EventHandler<NodeDeletedEventArgs> Deleted;
        public event EventHandler<NodeRenamedEventArgs> Renamed;
        public event EventHandler<NodeMovedEventArgs> Moved;
        public event EventHandler<NodeModifiedEventArgs> Modified;

        /// <summary>
        /// Raises the PreviewDeleted event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaisePreviewDeleted(PreviewNodeDeletedEventArgs e)
        {
            PreviewDeleted.RaiseAndValidate(this, e);
        }

        /// <summary>
        /// Raises the PreviewRenamed event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaisePreviewRenamed(PreviewNodeRenamedEventArgs e)
        {
            PreviewRenamed.RaiseAndValidate(this, e);
        }

        /// <summary>
        /// Raises the PreviewMoved event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaisePreviewMoved(PreviewNodeMovedEventArgs e)
        {
            PreviewMoved.RaiseAndValidate(this, e);
        }

        /// <summary>
        /// Raises the PreviewModified event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaisePreviewModified(PreviewNodeModifiedEventArgs e)
        {
            PreviewModified.RaiseAndValidate(this, e);
        }

        /// <summary>
        /// Raises the Deleted event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaiseDeleted(NodeDeletedEventArgs e)
        {
            Deleted.Raise(this, e);
        }

        /// <summary>
        /// Raises the Renamed event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaiseRenamed(NodeRenamedEventArgs e)
        {
            Renamed.Raise(this, e);
        }

        /// <summary>
        /// Raises the Moved event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaiseMoved(NodeMovedEventArgs e)
        {
            Moved.Raise(this, e);
        }

        /// <summary>
        /// Raises the Modified event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaiseModified(NodeModifiedEventArgs e)
        {
            Modified.Raise(this, e);
        }

        readonly IProjectIO _io;

        //This remains forever empty

        readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public FileNode(IProjectIO projectIO, string file) {
            _io = projectIO;
            FilePath = file;
            _logger.Trace("Created {0}: {1}", GetType().Name, file);
        }

        public override string ToString() {
            return FilePath;
        }

        protected string FilePath { get; set; }

        public string AbsolutePath {
            get {
                return _io.GetAbsolutePath(this.Path);
            }
        }

        public string Name {
            get {
                return System.IO.Path.GetFileName(FilePath);
            }
        }

        public byte[] Data {
            get {
                return _io.CachedReadRaw(FilePath);
            }
            set {
                var oldData = Data;
                RaisePreviewModified(new PreviewNodeModifiedEventArgs(oldData, value));
                _io.WriteFile(FilePath, value);
                RaiseModified(new NodeModifiedEventArgs(oldData, value));
            }
        }

        public string Text {
            get { return _io.CachedReadText(FilePath); }
            set { _io.WriteFile(FilePath, value); }
        }


        public void Rename(string newName) {
            if (newName == Name) return;

            var oldPath = FilePath;
            var newPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FilePath), newName);

            RaisePreviewRenamed(new PreviewNodeRenamedEventArgs(oldPath, newPath));

            _io.Move(FilePath, newPath);
            FilePath = newPath;
            RaiseRenamed(new NodeRenamedEventArgs(oldPath, FilePath));
        }

        public void Move(string newPath) {
            var oldPath = FilePath;
            var ultimateNewPath = System.IO.Path.Combine(newPath, System.IO.Path.GetFileName(FilePath));
            RaisePreviewMoved(new PreviewNodeMovedEventArgs(FilePath, ultimateNewPath));
            _io.Move(FilePath, ultimateNewPath);
            FilePath = ultimateNewPath;
            RaiseMoved(new NodeMovedEventArgs(oldPath, newPath));
        }


        public bool IsDeleted { get; private set; }

        public void Delete() {
            RaisePreviewDeleted(new PreviewNodeDeletedEventArgs());
            _io.Delete(FilePath);
            IsDeleted = true;
            RaiseDeleted(new NodeDeletedEventArgs());
        }


        public string Path {
            get {
                return FilePath;
            }
        }

        public ObservableCollection<IProjectNode> Children {
            get {
                return null;
            }
        }

        public void OpenInExplorer() {
            _io.OpenInExplorer(this);
        }

        public IProjectNode Parent { get; set; }
    }

    public class FileNodeDeletedException : Exception { }
    public class NothingToSaveException : Exception { }
    public class FileUnsavedChangesException : Exception { }
    public class FileMoveException : Exception { }

}
