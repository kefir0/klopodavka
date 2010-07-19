using System;

namespace KlopAi.algo
{
   /// <summary>
   /// Represents path node.
   /// </summary>
   public class Node : IComparable
   {
      #region Constructors

      public Node(int x, int y)
      {
         Parent = null;
         Hdist = 0;
         Gdist = 0;
         Cost = 0;
         X = x;
         Y = y;
      }

      public Node() : this(0, 0)
      {
      }

      #endregion

      #region IComparable Members

      public int CompareTo(object obj)
      {
         var n = obj as Node;
         if (n != null)
            return Fval.CompareTo(n.Fval);
         throw new ArgumentException("object is not a NODE");
      }

      #endregion

      #region Public properties and indexers

      /// <summary>
      /// Parent node; used to construct path.
      /// </summary>
      /// <value>The parent.</value>
      public Node Parent { get; set; }

      /// <summary>
      /// Cost of move from start Node to this Node.
      /// </summary>
      public double Gdist { get; set; }

      /// <summary>
      /// Heuristic cost of move from this Node to finish Node.
      /// </summary>
      public double Hdist { get; set; }

      /// <summary>
      /// Cost of move to this Node.
      /// </summary>
      public double Cost { get; set; }

      /// <summary>
      /// Gets or sets the X position of Node.
      /// </summary>
      public int X { get; set; }

      /// <summary>
      /// Gets or sets the Y position of Node.
      /// </summary>
      public int Y { get; set; }

      /// <summary>
      /// Resulting cost of moving to this Node.
      /// </summary>
      public double Fval
      {
         get { return Gdist + Hdist; }
      }

      #endregion

      #region Public methods

      public Node Clone()
      {
         return new Node(X, Y)
                   {
                      Parent = Parent,
                      Hdist = Hdist,
                      Gdist = Gdist,
                      Cost = Cost,
                   };
      }

      #endregion
   }
}