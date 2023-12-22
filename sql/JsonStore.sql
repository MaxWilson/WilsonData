create table JsonStore(
id int not null primary key,
owner nvarchar(300) not null,
category nvarchar(300),
json ntext
)
