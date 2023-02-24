using SFB;

public class StandaloneFileSelecter : FileSelecter {
	private ExtensionFilter[] _extFilters;

	public StandaloneFileSelecter() {
		/* Set ExtensionFilter for StandalonFileBrowser */
		//remove '.'
		_extFilters = new [] {
			new ExtensionFilter("Image/Video/Depth Files", Exts.AllExtsWithoutDot),
		};
	}
	
	public void SelectFile(OnPathSelected callback) {
		string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", _extFilters, false);
		if (paths.Length < 1)
			return;
		string path = paths[0];

		callback(path);
	}
	
	public void SelectDir(OnPathSelected callback) {
		string[] dirnames = StandaloneFileBrowser.OpenFolderPanel("Select a directory", null, false);
		if (dirnames.Length < 1)
			return;

		callback(dirnames[0]);
	}
}