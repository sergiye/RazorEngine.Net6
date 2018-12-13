using System;
using RazorEngine;
using RazorEngine.Templating; // For extension methods.

namespace ConsoleTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string template = "Hello @Model.Name, welcome to RazorEngine!";
            var result = Engine.Razor.RunCompile(template, "templateKey", null, new { Name = "World" });

            Console.WriteLine(result);
            Console.ReadKey();
        }
    }
}
