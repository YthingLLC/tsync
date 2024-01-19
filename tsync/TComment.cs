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

public struct TCommentData
{
    public string Text { get; init; }

    public TCommentData(string text)
    {
        Text = text;
    }
}

public struct TComment
{
    public string Id { get; init; }

    public DateTime Date { get; init; }

    public TMember MemberCreator { get; init; }

    //only like this to allow for built in .NET JSON deserialization, otherwise this would be flattened
    public TCommentData Data { get; init; }

    public TComment(string id, TMember memberCreator, TCommentData data)
    {
        Id = id;
        MemberCreator = memberCreator;
        Data = data;
    }

    public override string ToString()
    {
        return $"[tsync][{Date.ToString("u")}] {MemberCreator.FullName} ({MemberCreator.UserName}): {Data.Text}";
    }
}