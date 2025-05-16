# Puzzle #78 - Why So Slow?

Carl and Jeff want to know why their Blazor Server app is slow under a heavy load, and how to fix it.

YouTube Video: https://youtu.be/-RTbYcAuIWo

Blazor Puzzle Home Page: https://blazorpuzzle.com

## The Challenge

This is a seemingly simple Blazor Server app that retrieves a bunch of records from a SQL database. When we simulate a heavy load, it slows down. 

We want to know why and, most importantly, how to fix it.

## The Solution

There are two ways to speed this up, both with caching. 

### MemoryCache

A memory cache caches data, such as the results of a database query, in memory and allows it to remain cached for a period of time. This solution adds a memory cache to cache the data for a 1 second duration.

First, add this to *Program.cs*:

```c#
builder.Services.AddMemoryCache();
```

Now we can modify the `DataManager` to support the cache. Change it to this:

```c#
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace Puzzle78;

public class DataManager
{
	IConfiguration _configuration;
	private readonly IMemoryCache _cache;
	private readonly ILogger<DataManager> _logger;

	public DataManager(IConfiguration configuration, IMemoryCache cache, ILogger<DataManager> logger)
	{
		_cache = cache;
		_logger = logger;
		_configuration = configuration;
	}

	public async Task<List<Person>> GetAllPeople()
	{
#pragma warning disable CS8603 // Possible null reference return.
		return await _cache.GetOrCreateAsync("AllPeople", async entry =>
		{
			entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
			_logger.LogInformation("Hitting SQL at {Time}", DateTime.Now);

			var data = GetAllPeopleFromDB();
			return data;
		});
#pragma warning restore CS8603 // Possible null reference return.
	}

	public List<Person> GetAllPeopleFromDB()
	{
		var people = new List<Person>();
		var connectionString = _configuration["ConnectionStrings:DefaultConnection"];
		using (var connection = new SqlConnection(connectionString))
		{
			connection.Open();
			var command = new SqlCommand("SELECT Id, Name FROM Person", connection);
			using (var reader = command.ExecuteReader())
			{
				while (reader.Read())
				{
					var person = new Person
					{
						Id = reader.GetInt32(0),
						Name = reader.GetString(1)
					};
					people.Add(person);
				}
			}
		}
		return people;
	}

	public bool GenerateData()
	{
		var people = new List<Person>();
		for (int i = 0; i < 10000; i++)
		{
			people.Add(new Person { Id = i, Name = $"Person {i}" });
		}

		var connectionString = _configuration["ConnectionStrings:DefaultConnection"];

		using (var connection = new SqlConnection(connectionString))
		{
			connection.Open();
			using (var transaction = connection.BeginTransaction())
			{
				foreach (var person in people)
				{
					var command = new SqlCommand("INSERT INTO Person (Name) VALUES (@Name)", connection, transaction);
					command.Parameters.AddWithValue("@Name", person.Name);
					command.ExecuteNonQuery();
				}
				transaction.Commit();
			}
		}
		return true;
	}
}
```

First, we injected a `IMemoryCache` named `_cache`.

Next, we made our `GetAllPeople` method async.

We renamed our existing `GetAllPeople` method to `GetAllPeopleFromDB`

The magic happens here:

```c#
public async Task<List<Person>> GetAllPeople()
{
    return await _cache.GetOrCreateAsync("AllPeople", async entry =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1);
        _logger.LogInformation("Hitting SQL at {Time}", DateTime.Now);

        var data = GetAllPeopleFromDB();
        return data;
    });
}
```

This code basically reaches into the cache for data with the key "AllPeople". If it's either not there (first call) or has expired after one second, the data is returned from `GetAllPeopleFromDB` and the cache is refreshed.

#### app-runner

We also added a console app called **app-runner** which you can use to measure the time it takes for the app to return a 200. It connects 500 times and reports the time taken for each call.

Before adding the MemoryCache, results were coming back in a range of 300ms to 1300ms. After, we only had a long delay once a second, and most of the other results came back in less than 100ms.

### OutputCache

The second way to attach this is with an `OutputCache`. This caches the data returned by the app at the server level. If the data is in the cache, the web server returns the data, not executing the app code at all. In our test we added a one-second output cache to the app.

We added this to *Program.cs*:

```
builder.Services.AddOutputCache()
```

And we added this just after the line `app.UseAntiforgery();`

```
app.UseOutputCache();
```

Finally, we added this to the top of *Home.razor*:

```c#
@using Microsoft.AspNetCore.OutputCaching
@attribute [OutputCache(Duration = 1000)]
```

The results were even more dramatic than using the `MemoryCache` alone. Typical results came back in about 30ms.

Boom!