// У каждого теста свой контроллер и свои моки, общей базы нет — методы можно запускать параллельно.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]
