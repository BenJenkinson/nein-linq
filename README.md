﻿NeinLinq
========

*NeinLinq* provides helpful extensions for using LINQ providers supporting only a minor subset of .NET functions (like Entity Framework), reusing functions, rewriting queries (even making them null-safe), and building dynamic queries using (translatable) predicates / selectors.

To install *NeinLinq*, run the following command in the [NuGet Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console).

    PM> Install-Package NeinLinq

To run async queries within *Entity Framework 6* use the "special" build (there's an extra dependency for that; thus, the extra build).

    PM> Install-Package NeinLinq.EF6

To run async queries within *Entity Framework 7* use the "special" build (again, there's an extra dependency for that; thus, the extra build).

    PM> Install-Package NeinLinq.EF7

Lambda injection
----------------

Many LINQ providers can only support a very minor subset of .NET functionality, they cannot even support our own "functions". Say, we implement a simple method `LimitText` and use it within an ordinary LINQ query, which will get translated to SQL through *Entity Framework*...

> *LINQ to Entities does not recognize the method 'System.String LimitText(System.String, Int32)' method, and this method cannot be translated into a store expression.*

This is what we get; in fact, it's really annoying. We have to scatter our logic between code, that will be translated by any LINQ query provider, and code, that won't. It gets even worse: if some logic is "translatable", which is good, we have to copy and paste! Consolidating the code within an ordinary function does not work since the provider is unable to translate this simple method call. Meh.

Let us introduce "lambda injection":

```csharp
[InjectLambda]
public static string LimitText(this string value, int maxLength)
{
    if (value != null && value.Length > maxLength)
        return value.Substring(0, maxLength);
    return value;
}

public static Expression<Func<string, int, string>> LimitText()
{
    return (v, l) => v != null && v.Length > l ? v.Substring(0, l) : v;
}

// -------------------------------------------------------------------

from d in data.ToInjectable()
select new
{
    Id = d.Id,
    Value = d.Name.LimitText(10)
}
```

If a query is marked as "injectable" (`ToInjectable()`) and a function within this query is marked as "inject here" (`[InjectLambda]`), the rewrite engine of *NeinLinq* replaces the method call with the matching lambda expression, which can get translate to SQL or whatever. Thus, we are able to encapsulate unsupported .NET functionality and even create our own. Bazinga!

```csharp
[InjectLambda]
public static bool Like(this string value, string likePattern)
{
    throw new NotImplementedException();
}

public static Expression<Func<string, string, bool>> Like()
{
    return (v, p) => SqlFunctions.PatIndex(p, v) > 0;
}

// -------------------------------------------------------------------

from d in data.ToInjectable()
where d.Name.Like("%na_f%")
select ...
```

This is an example how we can abstract the `SqlFunctions` class of *Entity Framework* to use a (hopefully) nicer `Like` extension method within our code -- `PatIndex` is likely used to simulate a SQL LIKE statement, why not make it so? We can actually implement the "ordinary" method with the help of regular expressions to run our code *without* touching `SqlFunctions` too...

Finally, let us look at this query using *Entity Framework* or the like:

```csharp
from d in data.ToInjectable()
let e = d.RetrieveWhatever()
where d.FulfillsSomeCriteria()
select new
{
    Id = d.Id,
    Value = d.DoTheFancy(e)
}

// -------------------------------------------------------------------

[InjectLambda]
public static Whatever RetrieveWhatever(this Entity value)
{
    throw new NotImplementedException();
}

public static Expression<Func<Entity, Whatever>> RetrieveWhatever()
{
    return d => d.Whatevers.FirstOrDefault(e => ...);
}

[InjectLambda]
public static bool FulfillsSomeCriteria(this Entity value)
{
    throw new NotImplementedException();
}

public static Expression<Func<Entity, bool>> FulfillsSomeCriteria()
{
    return d => ...
}

[InjectLambda]
public static decimal DoTheFancy(this Entity value, Whatever other)
{
    throw new NotImplementedException();
}

public static Expression<Func<Entity, Whatever, decimal>> DoTheFancy()
{
    return (d, e) => ...
}
```

The methods `RetrieveWhatever`, `FulfillsSomeCriteria` and `DoTheFancy` should be marked accordingly, using the attribute `[InjectLambda]` or just the simple convention "same class, same name, matching signature" (which requires the class to be white listed by the way). And the call `ToInjectable` can happen anywhere within the LINQ query chain, so we don't have to pollute our business logic...

*Note:* that works with instance methods too.

Null-safe queries
-----------------

We are writing the year 2015 and still have to worry about null values.

Howsoever, we got used to it and we are fine. But writing queries in C# loaded with null checks doesn't feel right, it just looks awful, the translated SQL even gets worse. A LINQ query just for SQL dbs can spare these null checks, a LINQ query just for in-memory calculations must include them. And a LINQ query for both has a problem, which *NeinLinq* tries to solve.

The following query may trigger null references:

```csharp
from a in data
orderby a.SomeInteger
select new
{
    Year = a.SomeDate.Year,
    Integer = a.SomeOther.SomeInteger,
    Others = from b in a.SomeOthers
             select b.SomeDate.Month,
    More = from c in a.MoreOthers
           select c.SomeOther.SomeDate.Day
}
```

While the following query *should* not:

```csharp
from a in data
where a != null
orderby a.SomeInteger
select new
{
    Year = a.SomeDate.Year,
    Integer = a.SomeOther != null
            ? a.SomeOther.SomeInteger
            : 0,
    Others = a.SomeOthers != null
           ? from b in a.SomeOthers
             select b.SomeDate.Month
           : null,
    More = a.MoreOthers != null
         ? from c in a.MoreOthers
           select c.SomeOther != null
                ? c.SomeOther.SomeDate.Day
                : 0
         : null
}
```

Maybe we've forgot some check? Or we can relax thanks to *NeinLinq*:

```csharp
from a in data.ToNullsafe()
orderby a.SomeInteger
select new
{
    Year = a.SomeDate.Year,
    Integer = a.SomeOther.SomeInteger,
    Others = from b in a.SomeOthers
             select b.SomeDate.Month,
    More = from c in a.MoreOthers
           select c.SomeOther.SomeDate.Day
}
```

As with every `ToWhatever` helper within *NeinLinq*, `ToNullsafe` can be called wherever within the LINQ query chain.

Predicate translator
--------------------

Many data driven applications need to build some kind of dynamic queries. This can lead to dirty string manipulations, complex expression tree plumbing, or a combination of those. Simple *and*/*or*-conjunctions are already solved within other libraries, but conjunctions of "foreign" predicates are not that easy.

Let us think of three entities: Academy has Courses, Courses has Lectures.

```csharp
Expression<Func<Course, bool>> p = c => ...
Expression<Func<Course, bool>> q = c => ...

db.Courses.Where(p.And(q))...
```

Ok, we already know that.

```csharp
Expression<Func<Academy, bool>> p = a => ...

db.Courses.Where(p.Translate()
                  .To<Course>(c => c.Academy))...
```

We now can translate a (combined) predicate for a parent entity...

```csharp
Expression<Func<Lecture, bool>> p = l => ...

db.Courses.Where(p.Translate()
                  .To<Course>((c, q) => c.Lectures.Any(l => q(l))))...
```

..and even for child entities. Awesome!

Let us use all of this as a windup:

```csharp
IEnumerable<Expression<Func<Academy, bool>>> predicatesForAcademy = ...
IEnumerable<Expression<Func<Course, bool>>> predicatesForCourse = ...
IEnumerable<Expression<Func<Lecture, bool>>> predicatesForLecture = ...

var singlePredicateForAcademy =
    predicatesForAcademy.Aggregate((p, q) => p.And(q));
var singlePredicateForCourse =
    predicatesForCourse.Aggregate((p, q) => p.And(q));
var singlePredicateForLecture =
    predicatesForLecture.Aggregate((p, q) => p.And(q));

var academyPredicateForCourse =
    singlePredicateForAcademy.Translate()
                             .To<Course>(c => c.Academy);
var coursePredicateForCourse =
    singlePredicateForCourse; // the hard one ^^
var lecturePredicateForCourse =
    singlePredicateForLecture.Translate()
                             .To<Course>((c, p) => c.Lectures.Any(l => p(l)));

var finalPredicate =
    academyPredicateForCourse.And(coursePredicateForCourse)
                             .And(lecturePredicateForCourse);

db.Courses.Where(finalPredicate)...
```

In addition to it, no *Invoke* is used to achieve that: many LINQ providers do not support it (*Entity Framework*, i'm looking at you...), so this solution should be quite compatible.

Selector translator
-------------------

As with predicates selectors need some love too. If we've an existing selector for some base type and want to reuse this code for one or more concrete types, we're forced to copy and paste again. Don't do that!

Let us think of two entities (Academy and SuperAcademy) with according Contracts / ViewModels / DTOs / Whatever (AcademyView and SuperAcademyView).

```csharp
Expression<Func<Academy, AcademyView>> s =
    a => new AcademyView { Id = a.Id, Name = a.Name };
Expression<Func<SuperAcademy, SuperAcademyView>> t =
    a => new SuperAcademyView { Narf = a.Narf };
```

Note that we omit the *Member bindings* of the first selector within the second one. Don't repeat yourself, remember?

```csharp
db.Academies.OfType<SuperAcademy>()
            .Select(s.Translate()
                     .Cross<SuperAcademy>()
                     .Apply(t));
```

Although there're more options, the common scenario can look that way: reuse the base selector, start it's translation (type inference!), say where to start (no type inference), and finally apply the additional selector (type inference again!).

Now let us consider parent / child relations (Academy and Course).

```csharp
Expression<Func<Academy, AcademyView>> s =
    a => new AcademyView { Id = a.Id, Name = a.Name };
Expression<Func<Course, CourseView>> t =
    c => new CourseView { Id = c.Id, Name = c.Name };

db.Courses.Select(s.Translate()
                   .Cross<Course>(c => c.Academy)
                   .Apply(c => c.Academy, t));

db.Academies.Select(t.Translate()
                     .Cross<Academy>((a, v) => a.Courses.Select(c => v(c)))
                     .Apply(a => a.Courses, s));
```

Again, apart from other options, we can translate from parent to child: reuse the parent selector, start it's translation, say where to start (given the path to it's parent entity), and finally apply the additional selector (given the path to it's parent "view"). And we can translate the other way too: reuse the child selector, start it's translation, say where to start (given an expression to select the children), and finally apply the additional selector...

To be more flexible the "Source translation" / "Result translation" can be used individually:

```csharp
Expression<Func<Academy, AcademyView>> selectAcademy =
    a => new AcademyView { Id = a.Id, Name = a.Name };

var selectCourseWithAcademy =
    selectAcademy.Translate()
                 .Source<Course>(c => c.Academy)
                 .Translate()
                 .Result<CourseView>(c => c.Academy)
                 .Apply(a => new CourseView
                 {
                     Id = a.Id,
                     Name = a.Name
                 });
```

*Note:* for parent / child relations the less dynamic but (maybe) more readable *Lambda injection* is also an option: just encapsulate the selector as a nice extension method.

Function substitution
---------------------

This is a really dead simple one. Maybe we should've started here...

Just think of helper functions like the `SqlFunctions` class provided by *Entity Framework*. And we need to replace the whole class for unit testing or whatsoever.

```csharp
var query = ...

CallCodeUsingSqlFunctions(query
    .ToSubstitution(typeof(SqlFunctions), typeof(SqlCeFunctions)));
CallCodeUsingSqlFunctions(query
    .ToSubstitution(typeof(SqlFunctions), typeof(FakeFunctions)));
...
```

That's it.

Custom query manipulation
-------------------------

You want more? Okay, you can use the generic rewrite mechanism of this library to intercept LINQ queries with your own *Expression visitor*. The code behind the substitution above should provide a good example.

Dynamic query filtering / sorting
---------------------------------

At some point it may be necessary to filter / sort an almost ready query based on user input, which is by its nature not type safe but text based. To handle these scenarios as well a (very) simple helper is included.

```csharp
var query = data.Where("Name.Length", DynamicCompare.GreaterThan, "7")
                .OrderBy("Name").ThenBy("Number", descending: true);
```

It's possible to combine this stuff with the predicate translations above.

```csharp
var p = DynamicQuery.CreatePredicate<Whatever>("Name", "Contains", "p");
var q = DynamicQuery.CreatePredicate<Whatever>("Name", "Contains", "q");

var query = data.Where(p.Or(q));
```

*Note:* if you're seeking a possibility to create complex queries based on string manipulation, this won't help. The goal of this library is to stay type safe as long as possible.
