#region Copyright & License Information
/*
 * Copyright 2007-2011 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made 
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.RA.Widgets
{
	class SpecialPowerBinWidget : Widget
	{
		Dictionary<string, Sprite> spsprites;
		Animation ready;
		Animation clock;
		readonly List<Pair<Rectangle, Action<MouseInput>>> buttons = new List<Pair<Rectangle,Action<MouseInput>>>();

		readonly World world;
		readonly WorldRenderer worldRenderer;

		[ObjectCreator.UseCtor]
		public SpecialPowerBinWidget([ObjectCreator.Param] World world, [ObjectCreator.Param] WorldRenderer worldRenderer)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			spsprites = Rules.Info.Values.SelectMany( u => u.Traits.WithInterface<SupportPowerInfo>() )
				.Select(u => u.Image).Distinct()
				.ToDictionary(
					u => u,
					u => Game.modData.SpriteLoader.LoadAllSprites(u)[0]);

			ready = new Animation("pips");
			ready.PlayRepeating("ready");
			clock = new Animation("clock");
		}

		public override Rectangle EventBounds
		{
			get { return buttons.Any() ? buttons.Select(b => b.First).Aggregate(Rectangle.Union) : Bounds; }
		}

		public override bool HandleMouseInput(MouseInput mi)
		{			
			if (mi.Event == MouseInputEvent.Down)
			{
				var action = buttons.Where(a => a.First.Contains(mi.Location))
				.Select(a => a.Second).FirstOrDefault();
				if (action == null)
					return false;
		
				action(mi);
				return true;
			}

			return false;
		}		

		public override void Draw()
		{
			buttons.Clear();

			if( world.LocalPlayer == null ) return;

			var manager = world.LocalPlayer.PlayerActor.Trait<SupportPowerManager>();
			var powers = manager.Powers.Where(p => !p.Value.Disabled);
			var numPowers = powers.Count();
			if (numPowers == 0) return;

			var rectBounds = RenderBounds;
			WidgetUtils.DrawRGBA(WidgetUtils.GetChromeImage(world, "specialbin-top"),new float2(rectBounds.X,rectBounds.Y));
			for (var i = 1; i < numPowers; i++)
				WidgetUtils.DrawRGBA(WidgetUtils.GetChromeImage(world,"specialbin-middle"), new float2(rectBounds.X, rectBounds.Y + i * 51));
			WidgetUtils.DrawRGBA(WidgetUtils.GetChromeImage(world,"specialbin-bottom"), new float2(rectBounds.X, rectBounds.Y + numPowers * 51));

			// Hack Hack Hack
			rectBounds.Width = 69;
			rectBounds.Height = 10 + numPowers * 51 + 21;

			var y = rectBounds.Y + 10;
			foreach (var kv in powers)
			{
				var sp = kv.Value;
				var image = spsprites[sp.Info.Image];

				var drawPos = new float2(rectBounds.X + 5, y);
				var rect = new Rectangle(rectBounds.X + 5, y, 64, 48);

				if (rect.Contains(Viewport.LastMousePos))
				{
					var pos = drawPos.ToInt2();					
					var tl = new int2(pos.X-3,pos.Y-3);
					var m = new int2(pos.X+64+3,pos.Y+48+3);
					var br = tl + new int2(64+3+20,40);

					if (sp.TotalTime > 0)
						br += new int2(0,20);

					if (sp.Info.LongDesc != null)
						br += Game.Renderer.Fonts["Regular"].Measure(sp.Info.LongDesc.Replace("\\n", "\n"));
					else
						br += new int2(300,0);

					var border = WidgetUtils.GetBorderSizes("dialog4");

					WidgetUtils.DrawPanelPartial("dialog4", Rectangle.FromLTRB(tl.X, tl.Y, m.X + border[3], m.Y),
						PanelSides.Left | PanelSides.Top | PanelSides.Bottom | PanelSides.Center);
					WidgetUtils.DrawPanelPartial("dialog4", Rectangle.FromLTRB(m.X - border[2], tl.Y, br.X, m.Y + border[1]),
						PanelSides.Top | PanelSides.Right | PanelSides.Center);
					WidgetUtils.DrawPanelPartial("dialog4", Rectangle.FromLTRB(m.X, m.Y - border[1], br.X, br.Y),
						PanelSides.Left | PanelSides.Right | PanelSides.Bottom | PanelSides.Center);
					
					pos += new int2(77, 5);
					Game.Renderer.Fonts["Bold"].DrawText(sp.Info.Description, pos, Color.White);
					
					if (sp.TotalTime > 0)
					{
						pos += new int2(0,20);
						Game.Renderer.Fonts["Bold"].DrawText(WidgetUtils.FormatTime(sp.RemainingTime).ToString(), pos, Color.White);
						Game.Renderer.Fonts["Bold"].DrawText("/ {0}".F(WidgetUtils.FormatTime(sp.TotalTime)), pos + new int2(45,0), Color.White);			
					}
					
					if (sp.Info.LongDesc != null)
					{
						pos += new int2(0, 20);
						Game.Renderer.Fonts["Regular"].DrawText(sp.Info.LongDesc.Replace("\\n", "\n"), pos, Color.White);
					}
				}

				WidgetUtils.DrawSHP(image, drawPos, worldRenderer);

				clock.PlayFetchIndex("idle",
					() => sp.TotalTime == 0 ? clock.CurrentSequence.Length - 1 : (sp.TotalTime - sp.RemainingTime)
						* (clock.CurrentSequence.Length - 1) / sp.TotalTime);
				clock.Tick();

				WidgetUtils.DrawSHP(clock.Image, drawPos, worldRenderer);

				var overlay = sp.Ready ? "ready" : sp.Active ? null : "hold";
				if (overlay != null)
				{
					ready.Play(overlay);
					WidgetUtils.DrawSHP(ready.Image, drawPos + new float2((64 - ready.Image.size.X) / 2, 2), worldRenderer);
				}

				buttons.Add(Pair.New(rect,HandleSupportPower(kv.Key, manager)));

				y += 51;
			}
		}
		
		Action<MouseInput> HandleSupportPower(string key, SupportPowerManager manager)
		{
			return mi => { if (mi.Button == MouseButton.Left) manager.Target(key); };
		}
	}
}