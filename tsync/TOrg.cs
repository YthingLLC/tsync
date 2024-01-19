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

public struct TOrg
{
    public string Id { get; init; }
    public string DisplayName { get; init; }
    public List<string> IdBoards { get; init; }
    public string DomainName { get; init; }
    public int MembersCount { get; init; }

    public TOrg(string id, string displayName, List<string> idBoards, string domainName, int membersCount)
    {
        Id = id;
        DisplayName = displayName;
        IdBoards = idBoards;
        DomainName = domainName;
        MembersCount = membersCount;
    }
}