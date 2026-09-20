using System;
using System.Collections.Generic;
using System.Text;

namespace DemoMAFHarness.Tools
{
    internal sealed class WebBrowsingToolOptions
    {
        /// <summary>
        /// Gets or sets a list of host patterns that are always permitted, regardless of other settings.
        /// Patterns support wildcard prefix matching (e.g., <c>"*.example.com"</c> matches <c>"docs.example.com"</c>).
        /// Exact host names (e.g., <c>"docs.microsoft.com"</c>) are also supported.
        /// </summary>
        /// <remarks>This has the highest priority — if a host matches, it is allowed immediately.</remarks>
        public IReadOnlyList<string>? AllowedHosts { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether public internet hosts (non-private, non-loopback, non-link-local IPs) are permitted.
        /// Default is <see langword="false"/>.
        /// </summary>
        public bool AllowPublicNetworks { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether private network hosts are permitted.
        /// This includes RFC 1918 addresses (10.x.x.x, 172.16-31.x.x, 192.168.x.x),
        /// loopback (127.x.x.x, ::1), link-local (169.254.x.x, fe80::),
        /// and cloud metadata endpoints (169.254.169.254).
        /// Default is <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <b>Warning:</b> Enabling this allows the agent to make requests to internal services,
        /// localhost, and cloud metadata endpoints. Only enable this if you understand the SSRF risks.
        /// </remarks>
        public bool AllowPrivateNetworks { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether all hosts are permitted without any restriction.
        /// Default is <see langword="false"/>.
        /// </summary>
        /// <remarks>
        /// <b>⚠️ UNSAFE:</b> Enabling this disables all network boundary checks and allows the agent
        /// to access any URL, including internal services, cloud metadata endpoints, and localhost.
        /// Only use this for trusted, isolated environments where SSRF is not a concern.
        /// </remarks>
        public bool AllowAllHosts { get; set; }
    }
}
