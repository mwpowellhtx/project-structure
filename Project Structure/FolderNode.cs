using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using NLog;

namespace ProjectStructure {
    public interface IFolderNode : IProjectNode {
        event EventHandler<DirectoryRefreshedEventArgs> Refreshed;

        IFolderNode CreateSubFolder(string name);
        IFileNode CreateFile(string name, string content);
        IFileNode CreateFile(string name, byte[] content);

        void Refresh();

        bool IsDeleted { get; }
        bool IsRootNode { get; }
    }

    public class FolderNode : IFolderNode {
        public event EventHandler<DirectoryRefreshedEventArgs> Refreshed;

        public event EventHandler<PreviewNodeDeletedEventArgs> PreviewDeleted;
        public event EventHandler<PreviewNodeRenamedEventArgs> PreviewRenamed;
        public event EventHandler<PreviewNodeMovedEventArgs> PreviewMoved;
        public event EventHandler<PreviewNodeModifiedEventArgs> PreviewModified;
        
        public event EventHandler<NodeDeletedEventArgs> Deleted;
        public event EventHandler<NodeRenamedEventArgs> Renamed;
        public event EventHandler<NodeMovedEventArgs> Moved;
        public event EventHandler<NodeModifiedEventArgs> Modified;

        /// <summary>
        /// Raises the Refreshed event.
        /// </summary>
        /// <param name="e"></param>
        protected virtual void RaiseRefreshed(DirectoryRefreshedEventArgs e)
        {
            Refreshed.Raise(this, e);
        }

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
        readonly INodeFactory _nodeFactory;
        readonly bool _isRoot;

        readonly Logger _logger = LogManager.GetCurrentClassLogger();

        string _dirpath;

        readonly ObservableCollection<IProjectNode> _children = new ObservableCollection<IProjectNode>();

        public FolderNode(IProjectIO projectIO, INodeFactory nodeFactory, string dirpath, bool isRoot = false) {
            _io = projectIO;
            _nodeFactory = nodeFactory;
            _dirpath = dirpath;
            _isRoot = isRoot;

            _children.CollectionChanged += OnCollectionChanged;

            LoadFilesAndDirectories();
        }

        public override string ToString() {
            return Name;
        }

        public string AbsolutePath {
            get {
                return _io.GetAbsolutePath(Path);
            }
        }

        public string Name {
            get {
                return _isRoot ? _io.RootName : System.IO.Path.GetFileName(_dirpath);
            }
            set {
                Rename(value);
            }
        }

        public IFolderNode CreateSubFolder(string name) {
            var dirpath = System.IO.Path.Combine(_dirpath, name);
            _io.CreateDirectory(dirpath);
            return AddDirectory(dirpath);
        }

        public IFileNode CreateFile(string name, string content) {
            var filepath = System.IO.Path.Combine(_dirpath, name);
            _io.CreateFile(filepath, content);
            return AddFile(filepath);
        }

        public IFileNode CreateFile(string name, byte[] content) {
            var filepath = System.IO.Path.Combine(_dirpath, name);
            _io.CreateFile(filepath, content);
            return AddFile(filepath);
        }

        public void Delete() {
            RaisePreviewDeleted(new PreviewNodeDeletedEventArgs());
            _io.Delete(_dirpath);
            IsDeleted = true;
            RaiseDeleted(new NodeDeletedEventArgs());
        }


        public void Rename(string newName) {
            if (newName == Name) return;
            var oldpath = _dirpath;
            var rpath = RenamePath(newName);
            RaisePreviewRenamed(new PreviewNodeRenamedEventArgs(oldpath, rpath));
            _io.Move(_dirpath, rpath);
            _dirpath = rpath;

            foreach (var child in _children) {
                TakeOwnership(child);
            }

            RaiseRenamed(new NodeRenamedEventArgs(oldpath, _dirpath));
        }

        public void Move(string newPath) {
            var ultimateNewPath = System.IO.Path.Combine(newPath, System.IO.Path.GetFileName(_dirpath));

            var oldPath = Path;
            RaisePreviewMoved(new PreviewNodeMovedEventArgs(Path,ultimateNewPath));

            _io.Move(_dirpath, ultimateNewPath);

            _dirpath = ultimateNewPath;

            foreach (var child in _children) {
                TakeOwnership(child);
            }
            RaiseMoved(new NodeMovedEventArgs(oldPath,Path));
        }

        public void Refresh() {
            LoadFilesAndDirectories();
            RaiseRefreshed(new DirectoryRefreshedEventArgs(this));
        }

        public string Path {
            get { return _dirpath; }
        }

        public bool IsDeleted { get; private set; }

        public bool IsRootNode {
            get { return _isRoot; }
        }

        public ObservableCollection<IProjectNode> Children { get { return _children; } }

        IProjectNode _project;
        public IProjectNode Parent {
            get {
                return _project;
            }
            set {
                if (_isRoot) throw new Exception("Project cannot have parent");
                _project = value;
            }
        }

        public void OpenInExplorer() {
            _io.OpenInExplorer(this);
        }

        string RenamePath(string newName) {
            var dname = System.IO.Path.GetDirectoryName(_dirpath);
            return dname == null ? newName : System.IO.Path.Combine(dname, newName);
        }

        IFileNode AddFile(string filePath) {
            if (Children.Any(x => SamePath(x.Path, filePath))) return null;
            if (Children.Any(x => x.Name == System.IO.Path.GetFileName(filePath))) return null;
            var node = _nodeFactory.CreateFileNode(filePath);
            if (node != null) {
                _children.Add(node);
            }
            return node;
        }

        IFolderNode AddDirectory(string directory) {
            if (Children.Any(x => SamePath(x.Path, directory))) return null;
            if (Children.Any(x => x.Name == System.IO.Path.GetFileName(directory))) return null;
            var node = _nodeFactory.CreateFolderNode(directory);
            _children.Add(node);
            return node;
        }

        bool SamePath(string p1, string p2) {
            return p1 == p2 || ".\\" + p1 == p2 || p1 == p2 + ".\\";
        }

        void LoadFilesAndDirectories() {
            var directories = _io.ListDirectories(_dirpath);
            foreach (var directoryNode in Children.OfType<IFolderNode>().ToArray()) {
                if (directories.Any(x => x != directoryNode.Path)) {
                    _children.Remove(directoryNode);
                }
            }
            foreach (var newdir in directories) {
                var child = Children.OfType<IFolderNode>().FirstOrDefault(x => x.Path == _dirpath);
                if (child != null) {
                    child.Refresh();
                } else {
                    AddDirectory(newdir);
                }
            }

            var files = _io.ListFiles(_dirpath);
            foreach (var fileNode in Children.OfType<IFileNode>().ToArray()) {
                if (files.All(x => x != fileNode.Path)) {
                    _children.Remove(fileNode);
                }
            }
            foreach (var file in files) {
                if (Children.OfType<IFileNode>().Any(x => x.Path == file)) {
                    continue;
                }
                try {
                    AddFile(file);
                } catch(Exception ex) {
                    _logger.Error("Could not load file {0} because: {1}", file, ex.Message);
                }
            }

        }

        void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            switch (e.Action) {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems) {
                        if (ReferenceEquals(this, item)) {
                            throw new RecursiveFolderException();
                        }
                        if (item == null) {
                            throw new NullReferenceException("cannot add 'null' to folder children");
                        }
                        AddNode((IProjectNode) item);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems) {
                        RemoveNode((IProjectNode)item);
                    }
                    break;
                default:
                    throw new Exception("Unsupported collection event");
            }
        }


        void AddNode(IProjectNode node) {
            TakeOwnership(node);
            node.Parent = this;
            node.Deleted += HandleNodeDeleted;
        }

        void HandleNodeDeleted(object sender, NodeDeletedEventArgs e) {
            _children.Remove((IProjectNode)sender);
        }

        void RemoveNode(IProjectNode node) {
            node.Parent = null;
            node.Deleted -= HandleNodeDeleted;
        }

        void TakeOwnership(IProjectNode node) {
            if (!BelongsToThisFolder(node)) {
                MoveToThisFolder(node);
            }
        }

        bool BelongsToThisFolder(IProjectNode node) {
            if (IsRootNode) {
                return node.Path.Split(System.IO.Path.DirectorySeparatorChar).Length == 1 || node.Path.StartsWith(".\\");
            }
            return System.IO.Path.GetDirectoryName(node.Path) == _dirpath;
        }

        void MoveToThisFolder(IProjectNode node) {
            node.Move(_dirpath);
        }

    }

    public class InvalidRenameException : Exception { }
}