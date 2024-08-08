using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Настройка MongoDB
var mongoClient = new MongoClient("mongodb://localhost:27017");
var database = mongoClient.GetDatabase("ManageApp");
var usersCollection = database.GetCollection<User>("users");
var countersCollection = database.GetCollection<BsonDocument>("counters");

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Получение всех пользователей
app.MapGet("/api/users", async () =>
{
    var users = await usersCollection.Find(_ => true).ToListAsync();
    return Results.Ok(users);
});

// Получение одного пользователя по ID
app.MapGet("/api/users/{id}", async (string id) =>
{
    if (!ObjectId.TryParse(id, out var objectId))
    {
        return Results.BadRequest(new { message = "Invalid user ID." });
    }

    var user = await usersCollection.Find(u => u.Id == id).FirstOrDefaultAsync();
    return user is null ? Results.NotFound(new { message = "User not found." }) : Results.Ok(user);
});

// Создание нового пользователя
app.MapPost("/api/users", async (User user) =>
{
    user.Id = ObjectId.GenerateNewId().ToString(); // Генерация нового ID
    user.CreatedDate = DateTime.UtcNow; // Установка даты создания
    await usersCollection.InsertOneAsync(user);
    return Results.Created($"/api/users/{user.Id}", user);
});

// Обновление пользователя по ID
app.MapPut("/api/users/{id}", async (string id, User updatedUser) =>
{
    if (!ObjectId.TryParse(id, out var objectId))
    {
        return Results.BadRequest(new { message = "Invalid user ID." });
    }

    var filter = Builders<User>.Filter.Eq(u => u.Id, id);
    var update = Builders<User>.Update
        .Set(u => u.FirstName, updatedUser.FirstName)
        .Set(u => u.LastName, updatedUser.LastName)
        .Set(u => u.MiddleName, updatedUser.MiddleName)
        .Set(u => u.Email, updatedUser.Email)
        .Set(u => u.Roles, updatedUser.Roles)
        .Set(u => u.Password, updatedUser.Password);



    var options = new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After };

    var result = await usersCollection.FindOneAndUpdateAsync(filter, update, options);
    
    return result is null ? Results.NotFound(new { message = "User not found." }) : Results.Ok(result);
});

// Удаление пользователя по ID
app.MapDelete("/api/users/{id}", async (string id) =>
{
    if (!ObjectId.TryParse(id, out var objectId))
    {
        return Results.BadRequest(new { message = "Invalid user ID." });
    }

    var result = await usersCollection.FindOneAndDeleteAsync(u => u.Id == id);
    return result is null ? Results.NotFound(new { message = "User not found." }) : Results.Ok(result);
});

// Запуск приложения
app.Run();

public class User
{
    //[BsonId]
    //public ObjectId Id { get; set; } // ID пользователя в MongoDB

    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = "";

    public int Plant { get; set; } // Ссылка на завод
    public int Department { get; set; } // Ссылка на подразделение
    public int Position { get; set; } // Ссылка на должность
    public string Email { get; set; } = string.Empty; // Email пользователя
    public string LastName { get; set; } = string.Empty; // Фамилия
    public string FirstName { get; set; } = string.Empty; // Имя
    public string MiddleName { get; set; } = string.Empty; // Отчество
    public string Password { get; set; } = string.Empty; // Пароль (хранить зашифрованным!)
    public DateTime CreatedDate { get; set; } // Дата создания пользователя
    public List<string> Roles { get; set; } = new List<string>(); // Роли пользователя
}

public static class UserUtils
{
    public static async Task<int> GetNextSequenceValue(string sequenceName, IMongoCollection<BsonDocument> countersCollection)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", sequenceName);
        var update = Builders<BsonDocument>.Update.Inc("sequence_value", 1);
        var options = new FindOneAndUpdateOptions<BsonDocument>
        {
            ReturnDocument = ReturnDocument.After
        };

        var result = await countersCollection.FindOneAndUpdateAsync(filter, update, options);

        return result != null ? result["sequence_value"].AsInt32 : 1; 
    }
}
