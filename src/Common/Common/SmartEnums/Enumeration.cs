// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common.SmartEnums
{
  public abstract class Enumeration : IComparable
  {
    public string Name { get; private set; }

    public int Id { get; private set; }

    protected Enumeration(int id, string name)
    {
      Id = id;
      Name = name;
    }

    public override string ToString() => Name;

    public static IEnumerable<T> GetAll<T>() where T : Enumeration
    {
      var fields = typeof(T).GetFields(BindingFlags.Public |
                                       BindingFlags.Static |
                                       BindingFlags.DeclaredOnly);

      return fields.Select(f => f.GetValue(null)).Cast<T>();
    }

    public override bool Equals(object obj)
    {
      var otherValue = obj as Enumeration;

      if (otherValue == null)
        return false;

      var typeMatches = GetType().Equals(obj.GetType());
      var valueMatches = Id.Equals(otherValue.Id);

      return typeMatches && valueMatches;
    }

    public override int GetHashCode()
    {
      return Name.GetHashCode() ^ Id.GetHashCode();
    }

    public int CompareTo(object obj) => Id.CompareTo(((Enumeration)obj).Id);

    public static bool operator !=(Enumeration a, Enumeration b)
    {
      if ((a is null && b is not null) ||
          (a is not null && b is null))
      {
        return true;
      }

      if (a is not null && b is not null)
      {
        return a.Id != b.Id;
      }
      return false;
    }
    public static bool operator ==(Enumeration a, Enumeration b)
    {
      if (a is null && b is null)
      {
        return true;
      }

      if (a is not null && b is not null)
      {
        return a.Id == b.Id;
      }

      return false;
    }

    public static implicit operator string(Enumeration documentType) => documentType.Name;

    public static implicit operator int(Enumeration documentType) => documentType.Id;
  }
}
