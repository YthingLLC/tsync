/*
Copyright 2024 Ything LLC

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace tsync;

public struct FileMeta
{
    public Guid FileID { get; init; }

    public TAttachment AttachmentData { get; init; }

    public bool Complete { get; set; }

    public string? GraphUrl { get; set; }

    public string? Hash;

    public string OriginBoard { get; init; }

    public FileMeta(TAttachment attachmentData, string originBoard)
    {
        FileID = Guid.NewGuid();
        Complete = false;
        AttachmentData = attachmentData;
        OriginBoard = originBoard;
    }

    public FileMeta(Guid fileId, TAttachment attachmentData, string originBoard, bool complete = false,
        string? hash = null)
    {
        FileID = fileId;
        AttachmentData = attachmentData;
        Complete = complete;
        Hash = hash;
        OriginBoard = originBoard;
    }
}