namespace build_server
{
    public class BuildRequest
    {
        public string RepoUrl { get; set; }

        public string Branch { get; set; }

        public string Subdirectory { get; set; }
    }
}