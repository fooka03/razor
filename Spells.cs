using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Windows.Forms;

namespace Assistant
{
	public class Spell
	{
		public enum SpellFlag
		{
			None = '?',
			Beneficial = 'B',
			Harmful = 'H',
			Neutral = 'N',
		}

		readonly public SpellFlag Flag;
		readonly public int Circle;
		readonly public int Number;
		readonly public string WordsOfPower;
		readonly public string[] Reagents;

		private static Timer m_UnflagTimer;

		public Spell( char flag, int n, int c, string power, string[] reags )
		{
			Flag = (SpellFlag)flag;
			Number = n;
			Circle = c;
			WordsOfPower = power;
			Reagents = reags;
		}

		public int Name 
		{ 
			get
			{ 
				if ( Circle <= 8 ) // Mage
					return 3002011 + ( ( Circle - 1 ) * 8 ) + ( Number - 1 );
				else if ( Circle == 10 ) // Necr
					return 1060509 + Number - 1;
				else if ( Circle == 20 ) // Chiv
					return 1060585 + Number - 1;
				else if ( Circle == 40 ) // Bush
					return 1060595 + Number - 1; 
				else if ( Circle == 50 ) // Ninj
					return 1060610 + Number - 1; 
				else if ( Circle == 60 ) // Elfs
					return 1071026 + Number - 1;
				else
					return -1;
			}
		}

		public override string ToString()
		{
			return String.Format( "{0} (#{1})", Language.GetString( this.Name ), GetID() );
		}

		public int GetID()
		{
			return ToID( Circle, Number );
		}

		public int GetHue( int def )
		{
			if ( Config.GetBool( "ForceSpellHue" ) )
			{
				switch ( Flag )
				{
					case SpellFlag.Beneficial:
						return Config.GetInt( "BeneficialSpellHue" );
					case SpellFlag.Harmful:
						return Config.GetInt( "HarmfulSpellHue" );
					case SpellFlag.Neutral:
						return Config.GetInt( "NeutralSpellHue" );
					default:
						return def;
				}
			}
			else
			{
				return def;
			}
		}

		public void OnCast( PacketReader p )
		{
			Cast();
			ClientCommunication.SendToServer( p );
		}

		public void OnCast( Packet p )
		{
			Cast();
			ClientCommunication.SendToServer( p );
		}

		private void Cast()
		{
			if ( Config.GetBool( "SpellUnequip" ) && ClientCommunication.AllowBit( FeatureBit.UnequipBeforeCast ) )
			{
				Item pack = World.Player.Backpack;
				if ( pack != null )
				{
					// dont worry about uneqipping RuneBooks or SpellBooks
					Item item = World.Player.GetItemOnLayer( Layer.RightHand );
#if DEBUG
					if ( item != null && item.ItemID != 0x22C5 && item.ItemID != 0xE3B && item.ItemID != 0xEFA && !item.IsVirtueShield )
#else
					if ( item != null && item.ItemID != 0x22C5 && item.ItemID != 0xE3B && item.ItemID != 0xEFA )
#endif
					{
						DragDropManager.Drag( item, item.Amount );
						DragDropManager.Drop( item, pack );
					}

					item = World.Player.GetItemOnLayer( Layer.LeftHand );
#if DEBUG
					if ( item != null && item.ItemID != 0x22C5 && item.ItemID != 0xE3B && item.ItemID != 0xEFA && !item.IsVirtueShield )
#else
					if ( item != null && item.ItemID != 0x22C5 && item.ItemID != 0xE3B && item.ItemID != 0xEFA )
#endif
					{
						DragDropManager.Drag( item, item.Amount );
						DragDropManager.Drop( item, pack );
					}
				}
			}

			for(int i=0;i<Counter.List.Count;i++)
				((Counter)Counter.List[i]).Flag = false;

			if ( Config.GetBool( "HighlightReagents" ) )
			{
				for(int r=0;r<Reagents.Length;r++)
				{
					for(int i=0;i<Counter.List.Count;i++)
					{
						Counter c = (Counter)Counter.List[i];
						if ( c.Enabled && c.Format.ToLower() == Reagents[r] )
						{
							c.Flag = true;
							break;
						}
					}
				}

				if ( m_UnflagTimer != null )
					m_UnflagTimer.Stop();
				else
					m_UnflagTimer = new UnflagTimer();
				m_UnflagTimer.Start();
			}

			ClientCommunication.RequestTitlebarUpdate();
			ClientCommunication.PostSpellCast( this.Number );

			if ( World.Player != null )
			{
				World.Player.LastSpell = GetID();
				LastCastTime = DateTime.Now;
				Targeting.SpellTargetID = 0;
			}
		}

		public static DateTime LastCastTime = DateTime.MinValue;

		private class UnflagTimer : Timer
		{
			public UnflagTimer() : base( TimeSpan.FromSeconds( 30.0 ) )
			{
			}

			protected override void OnTick()
			{
				for(int i=0;i<Counter.List.Count;i++)
					((Counter)Counter.List[i]).Flag = false;
				ClientCommunication.RequestTitlebarUpdate();
			}
		}

		private static Hashtable m_SpellsByPower;
		private static Hashtable m_SpellsByID;
		private static HotKeyCallbackState HotKeyCallback;
		static Spell()
		{
			ArrayList list = new ArrayList();
			string filename = Path.Combine( Engine.BaseDirectory, "spells.def" );
			m_SpellsByPower = new Hashtable( 64 + 10 + 16 );
			m_SpellsByID = new Hashtable( 64 + 10 + 16 );

			if ( !File.Exists( filename ) )
			{
				MessageBox.Show( Engine.ActiveWindow, Language.GetString( LocString.NoSpells ), "Spells.def", MessageBoxButtons.OK, MessageBoxIcon.Warning );
				return;
			}

			using ( StreamReader reader = new StreamReader( filename ) )
			{
				string line;
				while ( (line=reader.ReadLine()) != null )
				{
					line = line.Trim();
					if ( line.Length <= 0 || line[0] == '#' )
						continue;
					string[] split = line.Split( '|' ); 

					try
					{
						if ( split.Length >= 5 )
						{
							string[] reags = new string[split.Length-5];
							for(int i=5;i<split.Length;i++)
								reags[i-5] = split[i].ToLower().Trim();
							Spell s = new Spell( split[0].Trim()[0], Convert.ToInt32( split[1].Trim() ), Convert.ToInt32( split[2].Trim() ), /*split[3].Trim(),*/ split[4].Trim(), reags );

							m_SpellsByID[s.GetID()] = s;

							if ( s.WordsOfPower != null && s.WordsOfPower.Trim().Length > 0 )
								m_SpellsByPower[s.WordsOfPower] = s;
						}
					}
					catch
					{
					}
				}
			}

			HotKeyCallback = new HotKeyCallbackState( OnHotKey );
			foreach ( Spell s in m_SpellsByID.Values )
				HotKey.Add( HKCategory.Spells, HKSubCat.SpellOffset+s.Circle, s.Name, HotKeyCallback, (ushort)s.GetID() );
			HotKey.Add( HKCategory.Spells, LocString.HealOrCureSelf, new HotKeyCallback( HealOrCureSelf ) );
		}

		public static void HealOrCureSelf()
		{
			Spell s = null;

			if ( World.Player.Poisoned )
				s = Get( 2, 3 );
			else if ( World.Player.Hits+30 < World.Player.HitsMax && World.Player.Mana >= 12 )
				s = Get( 4, 5 );
			else if ( World.Player.Hits < World.Player.HitsMax )
				s = Get( 1, 4 );

			if ( s != null )
			{
				Targeting.TargetSelf( true );
				ClientCommunication.SendToServer( new CastSpellFromMacro( (ushort)s.GetID() ) );
				s.Cast();
			}
		}

		public static void Initialize()
		{
			// no code, this is here to make sure out static ctor is init'd by the core
		}

		public static void OnHotKey( ref object state )
		{
			ushort id = (ushort)state;
			Spell s = Spell.Get( id );
			if ( s != null )
			{
				s.OnCast( new CastSpellFromMacro( id ) );
				//if ( Macros.MacroManager.AcceptActions )
				//	Macros.MacroManager.Action( new Macros.MacroCastSpellAction( s ) );
			}
		}

		public static int ToID( int circle, int num )
		{
			if ( circle < 10 )
				return ( ( circle - 1 ) * 8 ) + num;
			else
				return ( circle * 10 ) + num;
		}

		public static Spell Get( string power )
		{
			return m_SpellsByPower[power] as Spell;
		}

		public static Spell Get( int num )
		{
			return m_SpellsByID[num] as Spell;
		}

		public static Spell Get( int circle, int num )
		{
			return Get( Spell.ToID( circle, num ) );
		}
	}
}
