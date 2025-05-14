using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Puzzle78;

public class DataManager
{
	IConfiguration _configuration;
	public DataManager (IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public List<Person> GetAllPeople()
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
