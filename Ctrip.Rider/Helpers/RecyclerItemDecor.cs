﻿using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V7.Widget;
using Android.Views;

namespace Ctrip.Rider.Helpers
{
    public class RecyclerItemDecor : RecyclerView.ItemDecoration
    {
        private readonly Drawable mDivider;

        public RecyclerItemDecor(Drawable divider)
        {
            mDivider = divider;
        }

        public override void GetItemOffsets(Rect outRect, View view, RecyclerView parent, RecyclerView.State state)
        {
            base.GetItemOffsets(outRect, view, parent, state);

            if(parent.GetChildAdapterPosition(view) == 0)
            {
                return;
            }

            outRect.Top = mDivider.IntrinsicHeight;
        }

        public override void OnDraw(Canvas c, RecyclerView parent, RecyclerView.State state)
        {
            base.OnDraw(c, parent, state);

            int dividerLeft = 32;
            int dividerRight = parent.Width - 32;

            for(int i = 0; i < parent.ChildCount; i++)
            {
                if (i != parent.ChildCount - 1)
                {
                    View child = parent.GetChildAt(i);
                    RecyclerView.LayoutParams @params = (RecyclerView.LayoutParams)child.LayoutParameters;

                    int dividerTop = child.Bottom + @params.BottomMargin;
                    int dividerBottom = dividerTop + mDivider.IntrinsicHeight;

                    mDivider.SetBounds(dividerLeft, dividerTop, dividerRight, dividerBottom);
                    mDivider.Draw(c);
                }
            }
        }
    }
}