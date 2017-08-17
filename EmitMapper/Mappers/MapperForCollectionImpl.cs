using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using EmitMapper.AST;
using EmitMapper.AST.Helpers;
using EmitMapper.AST.Interfaces;
using EmitMapper.AST.Nodes;

namespace EmitMapper.Mappers
{
    /// <summary>
    ///     Mapper for collections. It can copy Array, List<>, ArrayList collections.
    ///     Collection type in source object and destination object can differ.
    /// </summary>
    internal class MapperForCollectionImpl : CustomMapperImpl
    {
        private ObjectsMapperDescr subMapper;

        protected MapperForCollectionImpl() : base(null, null, null, null, null)
        {
        }

        /// <summary>
        ///     Copies object properties and members of "from" to object "to"
        /// </summary>
        /// <param name="from">Source object</param>
        /// <param name="to">Destination object</param>
        /// <returns>Destination object</returns>
        internal override object MapImpl(object from, object to, object state)
        {
            if (to == null)
            {
                if (_targetConstructor != null)
                {
                    to = _targetConstructor.CallFunc();
                }
            }

            if (typeTo.IsArray)
            {
                if (from is IEnumerable)
                {
                    return CopyToArray((IEnumerable) from);
                }
                return CopyScalarToArray(@from);
            }
            if (typeTo.IsGenericType && typeTo.GetGenericTypeDefinition() == typeof (List<>))
            {
                if (@from is IEnumerable)
                {
                    return CopyToListInvoke((IEnumerable) @from);
                }
                return CopyToListScalarInvoke(@from);
            }
            if (typeTo == typeof (ArrayList))
            {
                if (@from is IEnumerable)
                {
                    return CopyToArrayList((IEnumerable) @from);
                }
                return CopyToArrayListScalar(@from);
            }
            if (typeof (IList).IsAssignableFrom(typeTo))
            {
                return CopyToIList((IList) to, @from);
            }
            return null;
        }

        private object CopyToIList(IList iList, object from)
        {
            if (iList == null)
            {
                iList = (IList) Activator.CreateInstance(typeTo);
            }
            foreach (var obj in @from is IEnumerable ? (IEnumerable) @from : new[] {@from})
            {
                if (obj == null)
                {
                    iList.Add(null);
                }
                if (_rootOperation == null || _rootOperation.ShallowCopy)
                {
                    iList.Add(obj);
                }
                else
                {
                    var Mapper = mapperMannager.GetMapperImpl(obj.GetType(), obj.GetType(), _mappingConfigurator);
                    iList.Add(Mapper.Map(obj));
                }
            }
            return iList;
        }

        /// <summary>
        ///     Copies object properties and members of "from" to object "to"
        /// </summary>
        /// <param name="from">Source object</param>
        /// <param name="to">Destination object</param>
        /// <returns>Destination object</returns>
        public override object Map(object from, object to, object state)
        {
            return base.Map(from, null, state);
        }

        /// <summary>
        ///     Returns true if specified type is supported by this Mapper
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        internal static bool IsSupportedType(Type type)
        {
            return
                type.IsArray ||
                type.IsGenericType && type.GetGenericTypeDefinition() == typeof (List<>) ||
                type == typeof (ArrayList) ||
                typeof (IList).IsAssignableFrom(type) ||
                typeof (IList<>).IsAssignableFrom(type)
                ;
        }

        /// <summary>
        ///     Creates an instance of Mapper for collections.
        /// </summary>
        /// <param name="MapperName">Mapper name. It is used for registration in Mappers repositories.</param>
        /// <param name="mapperMannager">Mappers manager</param>
        /// <param name="TypeFrom">Source type</param>
        /// <param name="TypeTo">Destination type</param>
        /// <param name="SubMapper"></param>
        /// <returns></returns>
        public static MapperForCollectionImpl CreateInstance(
            string MapperName,
            ObjectMapperManager mapperMannager,
            Type TypeFrom,
            Type TypeTo,
            ObjectsMapperDescr SubMapper,
            IMappingConfigurator mappingConfigurator
            )
        {
            var tb = DynamicAssemblyManager.DefineType(
                "GenericListInv_" + MapperName,
                typeof (MapperForCollectionImpl)
                );

            if (TypeTo.IsGenericType && TypeTo.GetGenericTypeDefinition() == typeof (List<>))
            {
                var methodBuilder = tb.DefineMethod(
                    "CopyToListInvoke",
                    MethodAttributes.Family | MethodAttributes.Virtual,
                    typeof (object),
                    new[] {typeof (IEnumerable)}
                    );

                InvokeCopyImpl(TypeTo, "CopyToList").Compile(new CompilationContext(methodBuilder.GetILGenerator()));

                methodBuilder = tb.DefineMethod(
                    "CopyToListScalarInvoke",
                    MethodAttributes.Family | MethodAttributes.Virtual,
                    typeof (object),
                    new[] {typeof (object)}
                    );

                InvokeCopyImpl(TypeTo, "CopyToListScalar").Compile(
                    new CompilationContext(methodBuilder.GetILGenerator())
                    );
            }

            var result = (MapperForCollectionImpl) Activator.CreateInstance(tb.CreateType());
            result.Initialize(mapperMannager, TypeFrom, TypeTo, mappingConfigurator, null);
            result.subMapper = SubMapper;

            return result;
        }

        private static IAstNode InvokeCopyImpl(Type copiedObjectType, string copyMethodName)
        {
            var mi = typeof (MapperForCollectionImpl).GetMethod(
                copyMethodName,
                BindingFlags.Instance | BindingFlags.NonPublic
                ).MakeGenericMethod(ExtractElementType(copiedObjectType));

            return new AstReturn
            {
                returnType = typeof (object),
                returnValue = AstBuildHelper.CallMethod(
                    mi,
                    AstBuildHelper.ReadThis(typeof (MapperForCollectionImpl)),
                    new List<IAstStackItem>
                    {
                        new AstReadArgumentRef {argumentIndex = 1, argumentType = typeof (object)}
                    }
                    )
            };
        }

        private static Type ExtractElementType(Type collection)
        {
            if (collection.IsArray)
            {
                return collection.GetElementType();
            }
            if (collection == typeof (ArrayList))
            {
                return typeof (object);
            }
            if (collection.IsGenericType && collection.GetGenericTypeDefinition() == typeof (List<>))
            {
                return collection.GetGenericArguments()[0];
            }
            return null;
        }

        internal static Type GetSubMapperTypeTo(Type to)
        {
            return ExtractElementType(to);
        }

        internal static Type GetSubMapperTypeFrom(Type from)
        {
            var result = ExtractElementType(from);
            if (result == null)
            {
                return from;
            }

            return result;
        }

        internal override object CreateTargetInstance()
        {
            return null;
        }

        private Array CopyToArray(IEnumerable from)
        {
            if (from is ICollection)
            {
                var result = Array.CreateInstance(typeTo.GetElementType(), ((ICollection) from).Count);
                var idx = 0;
                foreach (var obj in from)
                {
                    result.SetValue(subMapper.mapper.Map(obj), idx++);
                }
                return result;
            }
            else
            {
                var result = new ArrayList();
                foreach (var obj in from)
                {
                    result.Add(obj);
                }
                return result.ToArray(typeTo.GetElementType());
            }
        }

        private ArrayList CopyToArrayList(IEnumerable from)
        {
            if (ShallowCopy)
            {
                if (from is ICollection)
                {
                    return new ArrayList((ICollection) from);
                }
                var res = new ArrayList();
                foreach (var obj in @from)
                {
                    res.Add(obj);
                }
                return res;
            }

            var result = new ArrayList();
            if (from is ICollection)
            {
                result = new ArrayList(((ICollection) from).Count);
            }
            else
            {
                result = new ArrayList();
            }

            foreach (var obj in from)
            {
                if (obj == null)
                {
                    result.Add(null);
                }
                else
                {
                    var Mapper = mapperMannager.GetMapperImpl(obj.GetType(), obj.GetType(), _mappingConfigurator);
                    result.Add(Mapper.Map(obj));
                }
            }
            return result;
        }

        private ArrayList CopyToArrayListScalar(object from)
        {
            var result = new ArrayList(1);
            if (ShallowCopy)
            {
                result.Add(from);
                return result;
            }
            var Mapper = mapperMannager.GetMapperImpl(from.GetType(), from.GetType(), _mappingConfigurator);
            result.Add(Mapper.Map(from));
            return result;
        }

        protected List<T> CopyToList<T>(IEnumerable from)
        {
            List<T> result;
            if (from is ICollection)
            {
                result = new List<T>(((ICollection) from).Count);
            }
            else
            {
                result = new List<T>();
            }
            foreach (var obj in from)
            {
                result.Add((T) subMapper.mapper.Map(obj));
            }
            return result;
        }

        protected virtual object CopyToListInvoke(IEnumerable from)
        {
            return null;
        }

        protected List<T> CopyToListScalar<T>(object from)
        {
            var result = new List<T>(1);
            result.Add((T) subMapper.mapper.Map(from));
            return result;
        }

        protected virtual object CopyToListScalarInvoke(object from)
        {
            return null;
        }

        private Array CopyScalarToArray(object scalar)
        {
            var result = Array.CreateInstance(typeTo.GetElementType(), 1);
            result.SetValue(subMapper.mapper.Map(scalar), 0);
            return result;
        }
    }
}