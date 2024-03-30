#LD_LIBRARY_PATH=lib/ dotnet run > mapping.lst
LD_LIBRARY_PATH=lib/ dotnet run "$@"
