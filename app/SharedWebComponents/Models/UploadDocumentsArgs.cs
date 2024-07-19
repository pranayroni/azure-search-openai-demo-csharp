// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedWebComponents.Models;
public class UploadDocumentsArgs
{
    public IReadOnlyList<IBrowserFile> Files { get; set; }
    public string Category { get; set; }
    public bool Success { get; set; }
    public int? UploadedFilesCount { get; set; }
    public string? ErrorMessage { get; set; }
}
