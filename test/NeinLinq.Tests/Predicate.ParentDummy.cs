﻿using System.Collections.Generic;

namespace NeinLinq.Tests.Predicate
{
    public class ParentDummy : IDummy
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public IEnumerable<ChildDummy> Childs { get; set; }
    }
}
