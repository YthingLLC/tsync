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

public struct TList
{
    public string Id { get; init; }
    public string Name { get; init; }
    public bool Closed { get; init; }

    public List<TCard> Cards { get; init; }

    public TList(string id, string name, bool closed, List<TCard> cards)
    {
        Id = id;
        Name = name;
        Closed = closed;
        Cards = cards;
    }
}