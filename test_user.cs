using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Domain;
using Shared.Data;

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseMySql("Server=localhost;Port=3306;Database=rwfm_db;Uid=root;Pwd=12345;", ServerVersion.AutoDetect("Server=localhost;Port=3306;Database=rwfm_db;Uid=root;Pwd=12345;"));
using var db = new AppDbContext(optionsBuilder.Options);

var user = db.Users.OrderByDescending(u => u.Id).FirstOrDefault();
if (user != null)
{
    Console.WriteLine($"ID: {user.Id} | Code: {user.EmployeeCode} | Email: {user.Email} | Hash: {user.PasswordHash}");
    bool verifyDefault = Shared.Security.PasswordHasher.Verify("Password@123", user.PasswordHash);
    Console.WriteLine($"Verify with 'Password@123': {verifyDefault}");
}
else
{
    Console.WriteLine("No user found.");
}
