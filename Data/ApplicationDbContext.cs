using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Models;

namespace ShoeStore.Data;

// IdentityDbContext уже описывает пользователей, роли и связи между ними.
// Миграция создаёт семь таблиц Identity без отдельных DbSet для каждой из них.
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
}
