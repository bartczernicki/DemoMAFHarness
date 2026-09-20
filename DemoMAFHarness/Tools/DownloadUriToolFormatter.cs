using System;
using System.Collections.Generic;
using System.Text;
using Harness.Shared.Console.ToolFormatters;
using Microsoft.Extensions.AI;

namespace DemoMAFHarness.Tools
{
    public sealed class DownloadUriToolFormatter : ToolCallFormatter
    {
        /// <inheritdoc/>
        public override bool CanFormat(FunctionCallContent call) =>
            call.Name is "DownloadUri";

        /// <inheritdoc/>
        public override string? FormatDetail(FunctionCallContent call)
        {
            string? value = GetStringArgumentValue(call, "uri");
            return value is not null ? $"({value})" : null;
        }
    }
}
