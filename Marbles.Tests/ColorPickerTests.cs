namespace Marbles.Tests;

/// <summary>
/// The keys of the list of colors: a letter for each color (and Default), the same in all languages
/// </summary>
public class ColorPickerTests
{
    [Fact]
    public void EachColor_HasAKeyOfItsOwn_NotOneTheListUsesForMoving()
    {
        var keys = Enum.GetValues<ConsoleColor>().Select(ColorPicker.KeyOf).ToList();
        Assert.Equal(ColorPicker.Count, keys.Distinct().Count());
        Assert.DoesNotContain(ColorPicker.DefaultKey, keys);
        foreach (var moving in "GJKLQ") Assert.DoesNotContain(moving, keys);
        Assert.Equal('A', ColorPicker.KeyOf(ConsoleColor.Black));
        Assert.Equal('U', ColorPicker.KeyOf(ConsoleColor.White));
    }

    [Fact]
    public void AKey_IsTheColorOfIt_InUpperOrLowerCase()
    {
        foreach (var color in Enum.GetValues<ConsoleColor>())
        {
            Assert.Equal(color, ColorPicker.ColorOfKey(ColorPicker.KeyOf(color)));
            Assert.Equal(color, ColorPicker.ColorOfKey(char.ToLowerInvariant(ColorPicker.KeyOf(color))));
        }

        Assert.Null(ColorPicker.ColorOfKey('j'));
        Assert.Null(ColorPicker.ColorOfKey('1'));
    }
}
