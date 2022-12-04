namespace build_server
{
    public class BuildRequest
    {
        /// <summary>
        /// Source repository for the application to build. It can be a repository containing an 
        /// entire fork of the Flipper Zero firmware, but it can take longer to build.
        /// </summary>
        public string RepoUrl { get; set; }

        /// <summary>
        /// Branch in the repository to clone. Can be null or omitted to clone the HEAD. 
        /// </summary>
        public string Branch { get; set; } = null;

        /// <summary>
        /// Where to look in the source repo for the app source code. For repos originated from 
        /// a fork of the Flipper Zero firmware. Can be null or omitted if the app is at the 
        /// root of the repo.
        /// </summary>
        public string Subdirectory { get; set; } = null;

        /// <summary>
        /// Optional patch to apply. Must be a gzipped GIT patch file encoded as base64 string.
        /// </summary>
        public byte[] ApplyPatch { get; set; } = null;
    }
}