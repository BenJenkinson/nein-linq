﻿using System;

namespace NeinLinq.Tests.Selector
{
    public class ChildDummyView
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public ParentDummyView Parent { get; set; }
    }
}
