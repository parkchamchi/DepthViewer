using SimpleFileBrowser;

using UnityEngine;

public class SimpleFileSelecter : FileSelecter {
	public SimpleFileSelecter() {
		FileBrowser.DisplayedEntriesFilter += (entry) => {
			if (entry.IsDirectory)
				return true;

			foreach (string ext in Exts.AllExtsWithDot)
				if (entry.Name.EndsWith(ext))
					return true;

			return false;
		};
	}

	public void SelectFile(OnPathSelected callback) =>
		FileBrowser.ShowLoadDialog((paths) => callback(paths[0]), null, FileBrowser.PickMode.Files);

	public void SelectDir(OnPathSelected callback) =>
		FileBrowser.ShowLoadDialog((paths) => callback(paths[0]), null, FileBrowser.PickMode.Folders);
}