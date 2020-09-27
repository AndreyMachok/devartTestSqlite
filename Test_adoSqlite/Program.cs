using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
//using System.Data.SQLite;
using Devart.Data.SQLite;
using System.Data;

namespace Test_adoSqlite
{
    class Program
    {
        static SQLiteConnection sqLiteConnection1;
        static int FBook_idLength = 10;
        static int FBook_NameLength = 50;


        static void Main(string[] args)
        {
            string HelpText = "Команды: \r\n ? - Помощь\r\n 1-SELECT \r\n 2-INSERT\r\n 3-UPDATE\r\n 4-DELETE\r\n exit - Выход";
            sqLiteConnection1 = new SQLiteConnection();
            Console.WriteLine("Hello");
            string path = Directory.GetCurrentDirectory();
            sqLiteConnection1.ConnectionString = string.Format(@"Data Source={0}\DB\data.db3;FailIfMissing=False;",path);
            sqLiteConnection1.Open();
            using (SQLiteCommand Sqlcmd = new SQLiteCommand("PRAGMA foreign_keys=on;", sqLiteConnection1))
            {
                Sqlcmd.ExecuteNonQuery();
            }
            Console.WriteLine(HelpText);
            Console.Write("cmd>");
            string cmd = Console.ReadLine();
            while (cmd.ToLower() != "exit")
            {
                switch (cmd)
                {
                    case "1":
                        CMDSelect();
                        break;
                    case "2":
                        CMDInsert();
                        break;
                    case "3":
                        CMDUpdate();
                        break;
                    case "4":
                        CMDDelete();
                        break;
                    case "?":
                        Console.WriteLine(HelpText);
                        break;
                    default:
                        Console.WriteLine("Не известная команда");
                        break;
                }
                Console.Write("cmd>");
                cmd = Console.ReadLine();
            }
            sqLiteConnection1.Close();
        }

        static string NormalRow(string[] Values, int[] LenthFields)
        {
            if (LenthFields == null)
            {
                throw new ArgumentNullException(nameof(LenthFields));
            }
            string row = "";
            int l = 0;
            for (int i = 0; i < Values.Length; i++)
            {
                l = LenthFields[i] - Values[i].Length;
                row += "| " + Values[i];
                if (l > 0)
                {
                    row += String.Concat(Enumerable.Repeat(" ", l));
                }
            }
            row += "|";
            return row;
        }

        static void WriteTableLine()
        {
            Console.WriteLine(String.Concat(Enumerable.Repeat("-", FBook_idLength + FBook_NameLength + 5)));
        }

        static void WriteTable(string Caption, string SQL, string subSQL = "", string Pk = "", string subPk = "")
        {
            using (SQLiteCommand sqlCmd = new SQLiteCommand("", sqLiteConnection1))
            {
                sqlCmd.CommandText = SQL;
                SQLiteDataReader sqlReader = sqlCmd.ExecuteReader();
                Console.WriteLine(Caption);
                string row = NormalRow(new string[2] { sqlReader.GetName(0), sqlReader.GetName(1) }, new int[2] { FBook_idLength, FBook_NameLength });
                WriteTableLine();
                Console.WriteLine(row);
                WriteTableLine();
                while (sqlReader.Read())
                {
                    row = NormalRow(new string[2] { sqlReader[0].ToString(), sqlReader[1].ToString() }, new int[2] { FBook_idLength, FBook_NameLength });
                    Console.WriteLine(row);
                    WriteTableLine();
                    if (subSQL != "")
                        using (SQLiteCommand sqlCmd2 = new SQLiteCommand(subSQL, sqLiteConnection1))
                        {
                            sqlCmd2.Parameters.Clear();
                            sqlCmd2.Parameters.Add("@" + subPk, SqlDbType.Int);
                            sqlCmd2.Parameters["@" + subPk].Value = Convert.ToInt32(sqlReader[Pk]);
                            SQLiteDataReader sqlReader2 = sqlCmd2.ExecuteReader();
                            if (sqlReader2.HasRows)
                            {
                                while (sqlReader2.Read())
                                {
                                    row = NormalRow(new string[2] { sqlReader2[0].ToString(), sqlReader2[1].ToString() }, new int[2] { FBook_idLength, FBook_NameLength });
                                    Console.WriteLine(row);
                                }
                                WriteTableLine();
                            }
                            sqlReader2.Close();
                        }
                }
                WriteTableLine();
                sqlReader.Close();
            }
        }

        static void CMDSelect()
        {
            WriteTable("Список книг", "select id,name_book from books", "select '' id,a.name_author from Book_Author ba inner join Authors a on ba.id_author = a.id WHERE ba.id_book = @id", "id", "id");
        }

        static void CMDInsert()
        {
            using (SQLiteCommand sqlCmd = new SQLiteCommand("", sqLiteConnection1))
            {
                //вводим название
                Console.Write("Введите название новой книги>");
                string new_book = Console.ReadLine();
                if (new_book == "")
                {
                    Console.WriteLine("Название не должно быть пустым");
                    return;
                }
                //вводим автора
                WriteTable("Список авторов", "select id,name_author from authors", "");
                int autor_id = 0;
                Console.Write("Ведите ID автора или нажмите Enter>");
                List<int> author_list = new List<int>();
                string author = Console.ReadLine();
                while (author != "")
                {
                    if (!int.TryParse(author, out autor_id))
                    {
                        Console.WriteLine($"{author} - значение не является целочисленным.");
                    }
                    else
                    {
                        author_list.Add(autor_id);
                    }
                    Console.Write("Ведите еще одного ID автора или нажмите Enter>");
                    author = Console.ReadLine();
                }
                //сохраняем
                sqlCmd.Connection.BeginTransaction();
                try
                {
                    if (new_book.Length > FBook_NameLength)
                    {
                        Console.WriteLine($"Название будет усечено до {FBook_NameLength} символов");
                        new_book = new_book.Substring(0, FBook_NameLength);
                    }
                    sqlCmd.CommandText = "insert into books(name_book) values(@name);\r\n select cast(last_insert_rowid() as int) id";
                    sqlCmd.Parameters.Add("@name", SqlDbType.Text);
                    sqlCmd.Parameters["@name"].Value = new_book;
                    Int32 new_book_id = Convert.ToInt32(sqlCmd.ExecuteScalar());
                    if (new_book_id == 0) { throw new System.ArgumentException("ID пустой"); }
                    //сохраняем автора
                    if (author_list.Count() > 0)
                    {
                        foreach (int id_au in author_list)
                        {
                            InsertBookAuthor(new_book_id, id_au);
                        }
                    }
                    sqlCmd.Connection.Commit();
                    Console.WriteLine(string.Format("Книга {0} добавлена.", new_book));
                }
                catch (Exception ex)
                {
                    sqlCmd.Connection.Rollback();
                    Console.WriteLine("Ошибка добавления книги\r\n" + ex.Message);
                }
            }
        }
        static void InsertBookAuthor(int id_book, int id_author)
        {
            using (SQLiteCommand sqlCmd = new SQLiteCommand("insert into book_author(id_book,id_author) values(@id_book,@id_author)", sqLiteConnection1))
            {
                sqlCmd.Parameters.Add("@id_book", SqlDbType.Int);
                sqlCmd.Parameters.Add("@id_author", SqlDbType.Int);
                sqlCmd.Parameters["@id_book"].Value = id_book;
                sqlCmd.Parameters["@id_author"].Value = id_author;
                sqlCmd.ExecuteScalar();
            }
        }
        static void DeleteBookAuthor(int id_book, int id_author)
        {
            using (SQLiteCommand sqlCmd = new SQLiteCommand("delete from book_author where id_book=@id_book and id_author=@id_author", sqLiteConnection1))
            {
                sqlCmd.Parameters.Add("@id_book", SqlDbType.Int);
                sqlCmd.Parameters.Add("@id_author", SqlDbType.Int);
                sqlCmd.Parameters["@id_book"].Value = id_book;
                sqlCmd.Parameters["@id_author"].Value = id_author;
                sqlCmd.ExecuteScalar();
            }
        }
        static void CMDUpdate()
        {
            Console.Write("Введите ID книги для редактирования>");
            string book = Console.ReadLine();
            if (!int.TryParse(book, out int id_book))
            {
                Console.WriteLine("Не правильно ввели значение");
                return;
            }
            List<int> InsList = new List<int>();
            List<int> DelList = new List<int>();
            using (SQLiteCommand sqlCmd = new SQLiteCommand("", sqLiteConnection1))
            {
                sqlCmd.CommandText = "select name_book from books where id=@id";
                sqlCmd.Parameters.Add("@id", SqlDbType.Int);
                sqlCmd.Parameters["@id"].Value = id_book;
                string name_book = Convert.ToString(sqlCmd.ExecuteScalar());
                Console.WriteLine($"Книга '{name_book}'");
                Console.Write("Введите новое название или нажмите Enter>");
                name_book = Console.ReadLine();
                if (name_book != "") { Console.WriteLine($"Новое название книги {name_book}"); }
                WriteTable("Список авторов книги", $"select id_author,a.name_author from Book_Author ba inner join Authors a on ba.id_author = a.id WHERE ba.id_book = {id_book}");
                string cmd = "";
                string id_author;
                Console.Write("1 - Добавить автора\r\n2 - Удалить автора\r\nEnter - выход>");
                cmd = Console.ReadLine();
                while (cmd != "")
                {
                    switch (cmd)
                    {
                        case "1":
                            WriteTable("Список авторов", "select id,name_author from authors", "");
                            Console.Write("Введите ID автора>");
                            id_author = Console.ReadLine();
                            if (id_author != "") { InsList.Add(int.Parse(id_author)); }
                            break;
                        case "2":
                            Console.Write("Введите ID автора для удаления>");
                            id_author = Console.ReadLine();
                            if (id_author != "") { DelList.Add(int.Parse(id_author)); }
                            break;
                        default: Console.WriteLine("Команда выбрана не правильно"); break;
                    }
                    Console.Write("1 - Добавить автора\r\n2 - Удалить автора\r\nEnter - выход>");
                    cmd = Console.ReadLine();
                }
                sqlCmd.Connection.BeginTransaction();
                try
                {
                    if (name_book != "")
                    {
                        sqlCmd.CommandText = "update books set name_book=@name where id=@id";
                        sqlCmd.Parameters.Clear();
                        sqlCmd.Parameters.Add("@id", SqlDbType.Int);
                        sqlCmd.Parameters.Add("@name", SqlDbType.Text);
                        sqlCmd.Parameters["@id"].Value = id_book;
                        sqlCmd.Parameters["@name"].Value = name_book;
                        sqlCmd.ExecuteNonQuery();
                    }
                    foreach (int id_auth in InsList)
                    {
                        InsertBookAuthor(id_book, id_auth);
                    }
                    foreach (int id_auth in DelList)
                    {
                        DeleteBookAuthor(id_book, id_auth);
                    }
                    sqlCmd.Connection.Commit();
                    Console.WriteLine($"Книга {name_book} была изменена.");
                }
                catch (Exception ex)
                {
                    sqlCmd.Connection.Rollback();
                    Console.WriteLine("Ошибка обновления книги\r\n" + ex.Message);
                }
            }
        }
        static void CMDDelete()
        {
            using (SQLiteCommand sqlCmd = new SQLiteCommand("", sqLiteConnection1))
            {
                sqlCmd.CommandText = "select name_book from books where id=@id";
                sqlCmd.Parameters.Add("@id", SqlDbType.Int);
                Console.Write("Введите ID книги для удаления>");
                string id_book = Console.ReadLine();
                if (id_book == "")
                {
                    Console.WriteLine("ID не должно быть пустым");
                    return;
                }
                if (!int.TryParse(id_book, out int del_id))
                {
                    Console.WriteLine($"{id_book} - значение не является целочисленным.");
                    return;
                }
                sqlCmd.Connection.BeginTransaction();
                try
                {
                    //check book
                    sqlCmd.Parameters["@id"].Value = del_id;
                    string name_book = (string)sqlCmd.ExecuteScalar();
                    if (name_book == null)
                    {
                        throw new System.ArgumentException("Книга для удаления не найдена.");
                    }
                    //delete
                    sqlCmd.CommandText = "delete from books where id=@id";
                    sqlCmd.Parameters["@id"].Value = del_id;
                    sqlCmd.ExecuteScalar();
                    sqlCmd.Connection.Commit();
                    Console.WriteLine(string.Format("Книга '{0}' удалена.", name_book));
                }
                catch (Exception ex)
                {
                    sqlCmd.Connection.Rollback();
                    Console.WriteLine("Ошибка удаления книги\r\n" + ex.Message);
                }
            }
        }
    }

}
